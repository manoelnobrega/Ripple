namespace Ripple.Api.Dtos
{
    public record HistoricoResponse(string[] Labels, int[] Values, List<EventoResponse> Events);
}
