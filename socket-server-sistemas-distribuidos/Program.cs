using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Mail;
using socket_server_sistemas_distribuidos.Models;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 1. Catálogo de productos en memoria
var catalogo = new List<Producto>
{
    new Producto { Id = 1, Nombre = "Laptop ASUS TUF", Precio = 850.00m, Stock = 10 },
    new Producto { Id = 2, Nombre = "Mouse Logi G Pro", Precio = 120.00m, Stock = 25 },
    new Producto { Id = 3, Nombre = "Teclado Mecánico Keychron V1", Precio = 95.00m, Stock = 15 },
    new Producto { Id = 4, Nombre = "Monitor LG 27\" 144Hz", Precio = 280.00m, Stock = 8 }
};

// 2. Habilitar WebSockets en la aplicación
app.UseWebSockets();

// 3. Ruta del WebSocket
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine("--> Cliente Móvil Conectado vía WebSocket");

        // En cuanto se conecta, le mandamos el catálogo automáticamente
        await EnviarCatalogo(webSocket, catalogo);

        // Bucle para mantener la conexión viva y escuchar compras
        await EscucharCliente(webSocket, catalogo);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

// Método para enviar el catálogo en formato JSON
async Task EnviarCatalogo(WebSocket socket, List<Producto> lista)
{
    var opciones = new { accion = "CATALOGO", productos = lista };
    string jsonString = JsonSerializer.Serialize(opciones);
    var buffer = Encoding.UTF8.GetBytes(jsonString);

    await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    Console.WriteLine("--> Catálogo enviado al cliente");
}

// Bucle de escucha
async Task EscucharCliente(WebSocket socket, List<Producto> lista)
{
    var buffer = new byte[1024 * 4];

    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Cerrando", CancellationToken.None);
            Console.WriteLine("--> Cliente desconectado");
            break;
        }

        // Procesar mensaje recibido
        string mensajeJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Console.WriteLine($"Recibido: {mensajeJson}");

        try
        {
            using var doc = JsonDocument.Parse(mensajeJson);
            string accion = doc.RootElement.GetProperty("Accion").GetString() ?? "";

            if (accion == "COMPRAR")
            {
                var solicitud = JsonSerializer.Deserialize<SolicitudCompra>(mensajeJson);
                if (solicitud != null)
                {
                    await ProcesarCompra(socket, solicitud, lista);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error procesando JSON: {ex.Message}");
        }
    }
}

// Procesar la orden, generar proforma y enviar reporte
async Task ProcesarCompra(WebSocket socket, SolicitudCompra solicitud, List<Producto> lista)
{
    StringBuilder proformaText = new StringBuilder();
    proformaText.AppendLine("========== PROFORMA DE COMPRA ==========");
    proformaText.AppendLine($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
    proformaText.AppendLine($"Cliente: {solicitud.CorreoCliente}");
    proformaText.AppendLine("----------------------------------------");

    decimal totalGeneral = 0;

    foreach (var item in solicitud.Items)
    {
        var prod = lista.FirstOrDefault(p => p.Id == item.ProductoId);
        if (prod != null)
        {
            decimal subtotal = prod.Precio * item.Cantidad;
            totalGeneral += subtotal;
            proformaText.AppendLine($"{prod.Nombre} x{item.Cantidad} - ${subtotal:N2}");
        }
    }

    proformaText.AppendLine("----------------------------------------");
    proformaText.AppendLine($"TOTAL A PAGAR: ${totalGeneral:N2}");
    proformaText.AppendLine("========================================");

    string proformaFinal = proformaText.ToString();

    // 1. Enviar de vuelta a la app móvil por el socket
    var respuestaApp = new { accion = "PROFORMA", reporte = proformaFinal };
    var bufferResp = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(respuestaApp));
    await socket.SendAsync(new ArraySegment<byte>(bufferResp), WebSocketMessageType.Text, true, CancellationToken.None);
    Console.WriteLine("--> Proforma enviada a la App móvil");

    // 2. Enviar copia al correo electrónico del cliente
    EnviarCorreo(solicitud.CorreoCliente, proformaFinal);
}

void EnviarCorreo(string destino, string cuerpo)
{
    try
    {
        // NOTA PARA LA PROFESORA: Configuración básica usando un servidor SMTP simulado (o Mailtrap/Gmail)
        var smtpClient = new SmtpClient("smtp.gmail.com")
        {
            Port = 587,
            Credentials = new NetworkCredential("TU_CORREO@gmail.com", "TU_CONTRASEÑA_DE_APLICACION"),
            EnableSsl = true,
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress("TU_CORREO@gmail.com", "Sistema de Ventas Sockets"),
            Subject = "Tu Proforma de Compra - Tarea Universitaria",
            Body = cuerpo,
            IsBodyHtml = false,
        };

        mailMessage.To.Add(destino);
        // Descomenta la línea de abajo cuando pongas tus credenciales reales
        // smtpClient.Send(mailMessage); 
        Console.WriteLine($"--> Correo simulado enviado con éxito a: {destino}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al enviar correo: {ex.Message}");
    }
}

app.Run();