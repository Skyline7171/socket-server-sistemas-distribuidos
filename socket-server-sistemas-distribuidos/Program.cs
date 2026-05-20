using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web; // Necesario para el UnsafeRelaxedJsonEscaping
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

    // Forzamos el encoder relajado para que los nombres de productos lleven sus tildes bien mapeadas
    var opcionesJson = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    string jsonString = JsonSerializer.Serialize(opciones, opcionesJson);
    var buffer = Encoding.UTF8.GetBytes(jsonString);

    await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    Console.WriteLine("--> Catálogo enviado al cliente");
}

// Bucle de escucha y procesamiento
async Task EscucharCliente(WebSocket socket, List<Producto> lista)
{
    var buffer = new byte[1024 * 4];

    // DICCIONARIOS GLOBALES, Mantienen los datos vivos entre mensajes del socket
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
                    // Deserializar la solicitud completa para guardar los productos temporalmente
                    var solicitudCompra = JsonSerializer.Deserialize<SolicitudCompra>(mensajeJson);

                    if (solicitudCompra != null && !string.IsNullOrEmpty(solicitudCompra.CorreoCliente))
                    {
                        string correoCliente = solicitudCompra.CorreoCliente;

                        // Guardar el carrito en memoria para usarlo al verificar el código
                        carritosPendientes[correoCliente] = solicitudCompra;

                        // Generar un código aleatorio de 4 dígitos y guardarlo
                        string token = new Random().Next(1000, 9999).ToString();
                        codigosVerificacion[correoCliente] = token;

                        Console.WriteLine($"--> Código generado para {correoCliente}: {token}");

                        // Enviar el correo del código usando el método seguro UTF-8 para evitar errores en la palabra "Código"
                        string cuerpoCorreo = $"Tu código de verificación para procesar tu orden es: {token}";
                        EnviarCorreo(correoCliente, "Código de Verificación", cuerpoCorreo);

                        // Avisarle a la app móvil por el Socket que debe pedir el token
                        var respuestaTokenEnviado = new { accion = "PEDIR_CODIGO", correo = correoCliente };
                        var opcionesJson = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                        string jsonResp = JsonSerializer.Serialize(respuestaTokenEnviado, opcionesJson);
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
    // Validar que haya stock suficiente para TODO el carrito antes de descontar
    foreach (var item in solicitud.Items)
    {
        var prod = lista.FirstOrDefault(p => p.Id == item.ProductoId);
        if (prod == null)
        {
            await EnviarErrorStock(socket, $"El producto con ID {item.ProductoId} no existe en el catálogo.");
            return;
        }

        if (prod.Stock < item.Cantidad)
        {
            await EnviarErrorStock(socket, $"Stock insuficiente para '{prod.Nombre}'. Disponibles: {prod.Stock}, Solicitados: {item.Cantidad}");
            return;
        }
    }

    // Si todo está bien, procedemos a generar la proforma y descontar el stock real
    StringBuilder proformaText = new StringBuilder();
    proformaText.AppendLine("========== PROFORMA DE COMPRA ==========");
    proformaText.AppendLine($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
    proformaText.AppendLine($"Cliente: {solicitud.CorreoCliente}");
    proformaText.AppendLine("----------------------------------------");

    decimal totalGeneral = 0;

    foreach (var item in solicitud.Items)
    {
        var prod = lista.First(p => p.Id == item.ProductoId);

        prod.Stock -= item.Cantidad;

        decimal subtotal = prod.Precio * item.Cantidad;
        totalGeneral += subtotal;
        proformaText.AppendLine($"{prod.Nombre} x{item.Cantidad} - ${subtotal:N2} (Quedan: {prod.Stock})");

        Console.WriteLine($"[STOCK ACTUALIZADO] Producto: {prod.Nombre} | Nuevo Stock: {prod.Stock}");
    }

    proformaText.AppendLine("----------------------------------------");
    proformaText.AppendLine($"TOTAL A PAGAR: ${totalGeneral:N2}");
    proformaText.AppendLine("========================================");

    string proformaFinal = proformaText.ToString();

    // Enviar la proforma a la app móvil por el socket mapeando correctamente las tildes literales
    var respuestaApp = new { accion = "PROFORMA", reporte = proformaFinal };

    var opcionesJson = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    string jsonConTildes = JsonSerializer.Serialize(respuestaApp, opcionesJson);
    var bufferRespCorrecto = Encoding.UTF8.GetBytes(jsonConTildes);

    await socket.SendAsync(new ArraySegment<byte>(bufferRespCorrecto), WebSocketMessageType.Text, true, CancellationToken.None);
    Console.WriteLine("--> Proforma enviada a la App móvil con stock actualizado");

    // Enviar correo al cliente con la proforma
    EnviarCorreo(solicitud.CorreoCliente, "Proforma de Compra (Tarea Universitaria)", proformaFinal);

    // Le mandamos el catálogo actualizado inmediatamente a la app
    await EnviarCatalogo(socket, lista);
}

// Método auxiliar para avisar a la App de fallos de inventario
async Task EnviarErrorStock(WebSocket socket, string mensajeError)
{
    var respuestaError = new { accion = "ERROR_STOCK", detalle = mensajeError };
    var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(respuestaError));
    await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    Console.WriteLine($"--> Compra rechazada: {mensajeError}");
}

// Método global de correo parametrizado y protegido con UTF-8
void EnviarCorreo(string destino, string asunto, string cuerpo)
{
    try
    {
        using var client = new SmtpClient("sandbox.smtp.mailtrap.io", 2525)
        {
            Credentials = new NetworkCredential("92c6db5a8c37a3", "18e5c95bd15176"),
            EnableSsl = true
        };

        var mensaje = new MailMessage
        {
            From = new MailAddress("sockets-sistemas-distribuidos@gmail.com", "Sistema de Facturación"),
            Subject = asunto,
            Body = cuerpo,
            IsBodyHtml = false,

            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8,

            BodyTransferEncoding = System.Net.Mime.TransferEncoding.QuotedPrintable
        };

        mensaje.To.Add(destino);
        client.Send(mensaje);

        Console.WriteLine($"--> Correo simulado [{asunto}] enviado con éxito a: {destino}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al enviar correo: {ex.Message}");
    }
}

app.Run();