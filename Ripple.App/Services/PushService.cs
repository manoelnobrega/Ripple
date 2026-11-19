using System.Diagnostics;
using Plugin.FirebasePushNotifications;
using Plugin.FirebasePushNotifications.Model;

namespace Ripple.App.Services
{
    /// Pede permissão de notificação, registra o aparelho no Firebase e envia o token à API.
    /// O token pode mudar com o tempo, então o evento TokenRefreshed reenvia o novo valor.
    public class PushService
    {
        private const string ChaveResponsavel = "responsavelId";

        private readonly IFirebasePushNotification _fcm;
        private readonly INotificationPermissions _permissoes;
        private readonly ApiClient _api;
        private readonly SemaphoreSlim _trava = new(1, 1);

        private bool _iniciado;
        private string? _ultimoTokenEnviado;

        public PushService(IFirebasePushNotification fcm, INotificationPermissions permissoes, ApiClient api)
        {
            _fcm = fcm;
            _permissoes = permissoes;
            _api = api;
        }

        /// <summary>Chamar quando a primeira página já estiver na tela (o diálogo de permissão exige isso).</summary>
        public async Task InicializarAsync()
        {
            if (_iniciado) return;
            _iniciado = true;

            _fcm.TokenRefreshed += async (_, e) => await EnviarTokenAsync(e.Token);

            var status = await _permissoes.GetAuthorizationStatusAsync();
            if (status != AuthorizationStatus.Granted)
            {
                var concedida = await _permissoes.RequestPermissionAsync();
                if (!concedida)
                {
                    Debug.WriteLine("[Push] Permissão de notificação negada; o app não receberá avisos.");
                    return;
                }
            }

            await _fcm.RegisterForPushNotificationsAsync();

            // O token pode já estar pronto ou chegar depois pelo TokenRefreshed.
            await EnviarTokenAsync(_fcm.Token);
        }

        private async Task EnviarTokenAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;

            await _trava.WaitAsync();
            try
            {
                if (token == _ultimoTokenEnviado) return;

                var id = Preferences.Default.Get(ChaveResponsavel, 0);
                var atualizado = id != 0 && await _api.AtualizarTokenAsync(id, token);

                if (!atualizado)
                {
                    // Primeira vez (ou o responsável foi removido no servidor): cadastra este aparelho.
                    id = await _api.CadastrarAparelhoAsync(DeviceInfo.Current.Name, token);
                    Preferences.Default.Set(ChaveResponsavel, id);
                }

                _ultimoTokenEnviado = token;
                Debug.WriteLine($"[Push] Token enviado à API (responsável {id}).");
            }
            catch (Exception ex)
            {
                // Sem rede ou API fora do ar: tenta de novo na próxima abertura do app.
                Debug.WriteLine($"[Push] Falha ao enviar o token: {ex.Message}");
            }
            finally
            {
                _trava.Release();
            }
        }
    }
}
