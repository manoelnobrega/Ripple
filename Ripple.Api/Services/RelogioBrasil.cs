namespace Ripple.Api.Services;

/// <summary>
/// Relógio do sistema com o fuso de São Paulo como "horário local". Assim os serviços não
/// dependem do fuso da máquina onde a API roda, e os testes podem trocar o relógio.
/// </summary>
public class RelogioBrasil : TimeProvider
{
    private static readonly TimeZoneInfo Fuso = ObterFuso();

    public override TimeZoneInfo LocalTimeZone => Fuso;

    private static TimeZoneInfo ObterFuso()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.Local;
    }
}
