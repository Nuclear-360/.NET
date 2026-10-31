namespace Nuclear360.Curadoria.Domain;

/// <summary>Trilha de auditoria: quem fez o quê e quando.</summary>
public class EventoAuditoria
{
    public DateTime Quando { get; set; }
    public string Usuario { get; set; } = "";
    public string Acao { get; set; } = "";

    public EventoAuditoria() { }

    public EventoAuditoria(DateTime quando, string usuario, string acao)
    {
        Quando = quando;
        Usuario = usuario;
        Acao = acao;
    }
}
