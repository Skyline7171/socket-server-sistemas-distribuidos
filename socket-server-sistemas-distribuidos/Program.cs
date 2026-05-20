using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Mail;
using socket_server_sistemas_distribuidos.Models;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Catálogo de productos en memoria
var catalogo = new List<Producto>
{
    new Producto { Id = 1, Nombre = "Laptop ASUS TUF", Precio = 850.00m, Stock = 10 },
    new Producto { Id = 2, Nombre = "Mouse Logi G Pro", Precio = 120.00m, Stock = 25 },
    new Producto { Id = 3, Nombre = "Teclado Mecánico Keychron V1", Precio = 95.00m, Stock = 15 },
    new Producto { Id = 4, Nombre = "Monitor LG 27\" 144Hz", Precio = 280.00m, Stock = 8 }
};

// Habilitar WebSockets en la aplicación
app.UseWebSockets();

// Ruta del WebSocket
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

// Bucle de escucha y procesamiento
async Task EscucharCliente(WebSocket socket, List<Producto> lista)
{
    var buffer = new byte[1024 * 4];

    // DICCIONARIOS GLOBALES AL CICLO: Mantienen los datos vivos entre mensajes del socket
    var codigosVerificacion = new Dictionary<string, string>();
    var carritosPendientes = new Dictionary<string, SolicitudCompra>();

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
            using (JsonDocument jsonDoc = JsonDocument.Parse(mensajeJson))
            {
                string accion = jsonDoc.RootElement.GetProperty("Accion").GetString() ?? "";

                if (accion == "COMPRAR")
                {
                    // 1. Deserializar la solicitud completa para guardar los productos temporalmente
                    var solicitudCompra = JsonSerializer.Deserialize<SolicitudCompra>(mensajeJson);

                    if (solicitudCompra != null && !string.IsNullOrEmpty(solicitudCompra.CorreoCliente))
                    {
                        string correoCliente = solicitudCompra.CorreoCliente;

                        // 2. Guardar el carrito en memoria para usarlo al verificar el código
                        carritosPendientes[correoCliente] = solicitudCompra;

                        // 3. Generar un código aleatorio de 4 dígitos y guardarlo
                        string token = new Random().Next(1000, 9999).ToString();
                        codigosVerificacion[correoCliente] = token;

                        Console.WriteLine($"--> Código generado para {correoCliente}: {token}");

                        // 4. Enviar el código al Mailtrap del cliente
                        using var client = new SmtpClient("sandbox.smtp.mailtrap.io", 2525)
                        {
                            Credentials = new NetworkCredential("92c6db5a8c37a3", "18e5c95bd15176"),
                            EnableSsl = true
                        };

                        string cuerpoCorreo = $"Tu código de verificación para procesar tu orden es: {token}";
                        client.Send("sockets-sistemas-distribuidos@gmail.com", correoCliente, "Código de Verificación", cuerpoCorreo);

                        // 5. Avisarle a la app móvil por el Socket que debe pedir el token
                        var respuestaTokenEnviado = new { accion = "PEDIR_CODIGO", correo = correoCliente };
                        string jsonResp = JsonSerializer.Serialize(respuestaTokenEnviado);
                        var bufferResp = Encoding.UTF8.GetBytes(jsonResp);
                        await socket.SendAsync(new ArraySegment<byte>(bufferResp), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }

                if (accion == "VERIFICAR_CODIGO")
                {
                    string correoCliente = jsonDoc.RootElement.GetProperty("CorreoCliente").GetString() ?? "";
                    string codigoIngresado = jsonDoc.RootElement.GetProperty("Codigo").GetString() ?? "";

                    // Verificar si el código coincide con el que guardamos en memoria
                    if (codigosVerificacion.ContainsKey(correoCliente) && codigosVerificacion[correoCliente] == codigoIngresado)
                    {
                        Console.WriteLine($"--> ¡Código verificado con éxito para {correoCliente}!");

                        // Código correcto! Limpiamos el token
                        codigosVerificacion.Remove(correoCliente);

                        // Recuperamos el carrito que guardamos en el paso 1
                        if (carritosPendientes.TryGetValue(correoCliente, out var solicitudGuardada))
                        {
                            // Procesamos la compra real (Genera proforma, manda al celular y envía correo completo)
                            await ProcesarCompra(socket, solicitudGuardada, lista);

                            // Limpiamos el carrito de la memoria temporal
                            carritosPendientes.Remove(correoCliente);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"--> Código erróneo ingresado por: {correoCliente}");

                        // Código incorrecto, le avisamos a la app para que pinte la alerta roja
                        var respuestaError = new { accion = "CODIGO_ERRONEO" };
                        string jsonError = JsonSerializer.Serialize(respuestaError);
                        var bufferError = Encoding.UTF8.GetBytes(jsonError);
                        await socket.SendAsync(new ArraySegment<byte>(bufferError), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error procesando JSON o flujo de compra: {ex.Message}");
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

            // Opcional: Restar del stock en memoria para hacerlo aún más real
            if (prod.Stock >= item.Cantidad)
            {
                prod.Stock -= item.Cantidad;
            }
        }
    }

    proformaText.AppendLine("----------------------------------------");
    proformaText.AppendLine($"TOTAL A PAGAR: ${totalGeneral:N2}");
    proformaText.AppendLine("========================================");

    string proformaFinal = proformaText.ToString();

    // Enviar de vuelta a la app móvil por el socket
    var respuestaApp = new { accion = "PROFORMA", reporte = proformaFinal };
    var bufferResp = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(respuestaApp));
    await socket.SendAsync(new ArraySegment<byte>(bufferResp), WebSocketMessageType.Text, true, CancellationToken.None);
    Console.WriteLine("--> Proforma enviada a la App móvil");

    // Enviar copia final detallada al correo electrónico del cliente
    EnviarCorreo(solicitud.CorreoCliente, proformaFinal);
}

void EnviarCorreo(string destino, string cuerpo)
{
    try
    {
        using var client = new SmtpClient("sandbox.smtp.mailtrap.io", 2525)
        {
            Credentials = new NetworkCredential("92c6db5a8c37a3", "18e5c95bd15176"),
            EnableSsl = true
        };
        client.Send("sockets-sistemas-distribuidos@gmail.com", destino, "Proforma de Compra (Tarea Universitaria)", cuerpo);
        Console.WriteLine($"--> Correo simulado enviado con éxito a: {destino}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al enviar correo: {ex.Message}");
    }
}

app.Run();