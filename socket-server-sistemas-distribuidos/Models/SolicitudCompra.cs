namespace socket_server_sistemas_distribuidos.Models
{
    public class SolicitudCompra
    {
        public string Accion { get; set; } = "COMPRAR";
        public string CorreoCliente { get; set; } = string.Empty;
        public List<ItemCarrito> Items { get; set; } = new();
    }
}
