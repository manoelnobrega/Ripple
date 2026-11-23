using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace Ripple.App.Services;

/// <summary>
/// Fala com a boia (firmware ESP32-S3) por BLE usando o Nordic UART Service (NUS):
///   RX (app escreve comandos de texto: "status", "rearm", "test_alert"...)
///   TX (boia notifica uma linha de JSON terminada em '\n')
/// Quando a boia manda {"event":"FALL_ALERT",...}, o app registra a queda na Ripple.Api
/// (que dispara o push para os responsáveis) e avisa a tela via mensagem para o JavaScript.
/// </summary>
public class RippleBleService
{
    // UUIDs iguais aos de ble_ripple.h no firmware
    private static readonly Guid NusServico = Guid.Parse("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    private static readonly Guid NusRx = Guid.Parse("6e400002-b5a3-f393-e0a9-e50e24dcca9e"); // app -> boia
    private static readonly Guid NusTx = Guid.Parse("6e400003-b5a3-f393-e0a9-e50e24dcca9e"); // boia -> app

    private readonly ApiClient _api;
    private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;
    private readonly StringBuilder _buffer = new();
    private readonly object _bufferTrava = new();

    private CancellationTokenSource? _cts;
    private IDevice? _dispositivo;
    private ICharacteristic? _rx;
    private TaskCompletionSource _desconectou = NovoTcs();

    /// <summary>JSON pronto para ir à tela (HybridWebView.SendRawMessage). Pode vir de qualquer thread.</summary>
    public event Action<string>? MensagemParaTela;

    /// <summary>desconectada | procurando | conectando | conectada</summary>
    public string Estado { get; private set; } = "desconectada";
    public bool Conectada => Estado == "conectada";

    private void MudarEstado(string estado, string? motivo = null)
    {
        if (Estado == estado) return;
        Estado = estado;
        Enviar(new { tipo = "conexao", estado, motivo });
    }

    public RippleBleService(ApiClient api)
    {
        _api = api;
        _adapter.DeviceDisconnected += OnDesconectou;
        _adapter.DeviceConnectionLost += OnDesconectou;
    }

    // ---------------------------------------------------------------- ciclo de vida

    /// <summary>
    /// Chamado pelo botão "Conectar com a boia". Pede permissão e começa a procurar/conectar;
    /// reconecta sozinho se a conexão cair. Devolve o texto do erro, ou null se começou.
    /// </summary>
    public async Task<string?> IniciarAsync()
    {
        if (_cts is not null) return null;

        if (!CrossBluetoothLE.Current.IsAvailable)
            return "Este aparelho não tem Bluetooth LE.";
        if (!await GarantirPermissoesAsync())
            return "Permissão de Bluetooth negada. Libere nas configurações do app.";
        if (CrossBluetoothLE.Current.State != BluetoothState.On)
            return "Ligue o Bluetooth do celular e tente de novo.";

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        MudarEstado("procurando");
        _ = Task.Run(() => LoopAsync(ct));
        return null;
    }

    public async Task PararAsync()
    {
        _cts?.Cancel();
        _cts = null;
        if (_dispositivo is not null)
        {
            try { await _adapter.DisconnectDeviceAsync(_dispositivo); } catch { /* já caiu */ }
        }
        LimparConexao();
        MudarEstado("desconectada");
    }

    private static async Task<bool> GarantirPermissoesAsync()
    {
        // O diálogo de permissão precisa ser aberto na thread principal (o JS chama por outra thread).
        var status = await MainThread.InvokeOnMainThreadAsync(async () =>
            OperatingSystem.IsAndroidVersionAtLeast(31)
                ? await Permissions.RequestAsync<Permissions.Bluetooth>()           // BLUETOOTH_SCAN + CONNECT
                : await Permissions.RequestAsync<Permissions.LocationWhenInUse>()); // Android 11-: scan exige localização
        return status == PermissionStatus.Granted;
    }

    // ---------------------------------------------------------------- loop de conexão

    private async Task LoopAsync(CancellationToken ct)
    {
        var espera = 2;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                MudarEstado("procurando");
                var boia = await ProcurarAsync(ct);
                if (boia is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(espera), ct);
                    espera = Math.Min(espera * 2, 15);
                    continue;
                }

                await ConectarAsync(boia, ct);
                espera = 2;

                // Fica aqui enquanto estiver conectado; OnDesconectou libera.
                await _desconectou.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BLE] Falha no ciclo: {ex.Message}");
                try { await Task.Delay(TimeSpan.FromSeconds(espera), ct); } catch (OperationCanceledException) { break; }
                espera = Math.Min(espera * 2, 15);
            }
            finally
            {
                LimparConexao();
            }
        }
    }

    // A boia anuncia "Ripple-Buoy" em monitoramento e "RIPPLE: QUEDA NA PISCINA!" em alerta.
    private static bool EhBoia(IDevice d) =>
        d.Name is { } n && (n.StartsWith("Ripple-Buoy", StringComparison.Ordinal)
                            || n.StartsWith("RIPPLE:", StringComparison.Ordinal));

    private async Task<IDevice?> ProcurarAsync(CancellationToken ct)
    {
        IDevice? achado = null;
        using var scanCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        void Handler(object? s, DeviceEventArgs e)
        {
            if (achado is null && EhBoia(e.Device))
            {
                achado = e.Device;
                scanCts.Cancel();
            }
        }

        _adapter.DeviceDiscovered += Handler;
        try
        {
            _adapter.ScanTimeout = 10_000;
            await _adapter.StartScanningForDevicesAsync(cancellationToken: scanCts.Token);
        }
        catch (OperationCanceledException) { /* achou ou acabou o tempo */ }
        finally
        {
            _adapter.DeviceDiscovered -= Handler;
            try { await _adapter.StopScanningForDevicesAsync(); } catch { }
        }

        ct.ThrowIfCancellationRequested();
        return achado;
    }

    private async Task ConectarAsync(IDevice boia, CancellationToken ct)
    {
        _desconectou = NovoTcs();
        _dispositivo = boia;
        MudarEstado("conectando");

        await _adapter.ConnectToDeviceAsync(boia, cancellationToken: ct);

        // Sem isso o Android fica com MTU 23 (20 bytes úteis) e os JSON de ~250 bytes da boia chegam truncados.
        // O firmware já aceita até 512 (CONFIG_BT_NIMBLE_ATT_PREFERRED_MTU).
        await boia.RequestMtuAsync(512);

        var servico = await boia.GetServiceAsync(NusServico, ct)
                      ?? throw new InvalidOperationException("Serviço NUS não encontrado na boia.");
        _rx = await servico.GetCharacteristicAsync(NusRx)
              ?? throw new InvalidOperationException("Característica RX não encontrada.");
        var tx = await servico.GetCharacteristicAsync(NusTx)
                 ?? throw new InvalidOperationException("Característica TX não encontrada.");

        tx.ValueUpdated += OnTxRecebido;
        await tx.StartUpdatesAsync();

        MudarEstado("conectada");
        Debug.WriteLine($"[BLE] Conectado em {boia.Name} ({boia.Id}).");

        // Sincroniza: se a boia já estava em alerta quando conectamos, a tela precisa saber.
        await EnviarComandoAsync("status");
    }

    private void OnDesconectou(object? sender, DeviceErrorEventArgs e)
    {
        if (_dispositivo is not null && e.Device.Id == _dispositivo.Id)
            _desconectou.TrySetResult();
    }

    private void LimparConexao()
    {
        _rx = null;
        _dispositivo = null;
        lock (_bufferTrava) _buffer.Clear();
    }

    private static TaskCompletionSource NovoTcs() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // ---------------------------------------------------------------- comandos (app -> boia)

    /// <summary>Envia um comando de texto do firmware: status, rearm, test_alert, calibrate, sens 0.30, battery...</summary>
    public async Task<bool> EnviarComandoAsync(string comando)
    {
        var rx = _rx;
        if (rx is null || !Conectada) return false;
        try
        {
            await rx.WriteAsync(Encoding.UTF8.GetBytes(comando)); // o firmware rejeita >= 120 bytes
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BLE] Falha ao enviar '{comando}': {ex.Message}");
            return false;
        }
    }

    // ---------------------------------------------------------------- mensagens (boia -> app)

    private void OnTxRecebido(object? sender, CharacteristicUpdatedEventArgs e)
    {
        var pedaco = Encoding.UTF8.GetString(e.Characteristic.Value);
        var linhas = new List<string>();

        lock (_bufferTrava)
        {
            _buffer.Append(pedaco);
            var texto = _buffer.ToString();
            int i;
            var inicio = 0;
            while ((i = texto.IndexOf('\n', inicio)) >= 0)
            {
                linhas.Add(texto[inicio..i].Trim());
                inicio = i + 1;
            }
            _buffer.Clear();
            _buffer.Append(texto[inicio..]);
        }

        foreach (var linha in linhas)
            if (linha.StartsWith('{'))
                _ = ProcessarLinhaAsync(linha);
    }

    private async Task ProcessarLinhaAsync(string linha)
    {
        try
        {
            using var doc = JsonDocument.Parse(linha);
            var raiz = doc.RootElement;
            var evento = raiz.TryGetProperty("event", out var ev) ? ev.GetString() : null;
            var estado = raiz.TryGetProperty("state", out var st) ? st.GetString() : null;

            switch (evento)
            {
                case "FALL_ALERT":
                    // Mostra na tela primeiro; o registro na API (que dispara o push) vem em seguida, com retry.
                    Enviar(new { tipo = "queda" });
                    await RegistrarNaApiComRetryAsync("queda");
                    break;

                case "FALL_ALERT_TEST":
                    Enviar(new { tipo = "teste" });
                    await RegistrarNaApiComRetryAsync("test");
                    break;

                case "STATE_MONITORING":
                case "REARMED":
                    Enviar(new { tipo = "normal" });
                    break;

                case "CRITICAL_BATTERY_SHUTDOWN":
                    Enviar(new { tipo = "bateria_critica" });
                    break;

                case null when estado == "FALL_ALERT":
                    // Resposta a "status" com a boia já em alerta (ex.: caiu enquanto o app estava desconectado).
                    // Só atualiza a tela; não registra de novo para não duplicar na API.
                    Enviar(new { tipo = "queda" });
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BLE] Linha ignorada ({ex.Message}): {linha}");
        }
    }

    private async Task RegistrarNaApiComRetryAsync(string tipo)
    {
        int[] esperas = { 1, 3, 8 };
        for (var tentativa = 0; ; tentativa++)
        {
            try
            {
                await _api.RegistrarEventoAsync(tipo, simulado: false);
                return;
            }
            catch (Exception ex) when (tentativa < esperas.Length)
            {
                Debug.WriteLine($"[BLE] API falhou ({ex.Message}); nova tentativa em {esperas[tentativa]}s.");
                await Task.Delay(TimeSpan.FromSeconds(esperas[tentativa]));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BLE] Não foi possível registrar o evento '{tipo}': {ex.Message}");
                Enviar(new { tipo = "erro_api" });
                return;
            }
        }
    }

    private void Enviar(object mensagem) =>
        MensagemParaTela?.Invoke(JsonSerializer.Serialize(mensagem));
}
