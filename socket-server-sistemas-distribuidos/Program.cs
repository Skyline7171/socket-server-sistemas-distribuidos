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
    new Producto { Id = 1, Nombre = "Laptop ASUS TUF Gaming", Precio = 850.00m, Stock = 10, ImagenUrl = "https://images.unsplash.com/photo-1603302576837-37561b2e2302?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 2, Nombre = "Mouse Logi G Pro Wireless", Precio = 120.00m, Stock = 25, ImagenUrl = "https://images.unsplash.com/photo-1615663245857-ac93bb7c39e7?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 3, Nombre = "Teclado Mecánico Keychron V1", Precio = 95.00m, Stock = 15, ImagenUrl = "https://images.unsplash.com/photo-1595225476474-87563907a212?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 4, Nombre = "Monitor LG 27\" UltraGear 144Hz", Precio = 280.00m, Stock = 8, ImagenUrl = "https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 5, Nombre = "Auriculares HyperX Cloud II", Precio = 89.99m, Stock = 30, ImagenUrl = "https://images.unsplash.com/photo-1606220588913-b3aacb4d2f46?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 6, Nombre = "Tarjeta Gráfica RTX 4070 Ti", Precio = 799.00m, Stock = 5, ImagenUrl = "https://images.unsplash.com/photo-1591488320449-011701bb6704?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 7, Nombre = "Procesador AMD Ryzen 7 7800X3D", Precio = 389.00m, Stock = 12, ImagenUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 8, Nombre = "Memoria RAM Corsair Vengeance 32GB DDR5", Precio = 115.00m, Stock = 20, ImagenUrl = "https://images.unsplash.com/photo-1541029071515-84cc54f84dc5?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 9, Nombre = "Disco SSD NVMe WD Black 1TB", Precio = 85.50m, Stock = 18, ImagenUrl = "https://plus.unsplash.com/premium_photo-1721133221361-4f2b2af3b6fe?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D" },
    new Producto { Id = 10, Nombre = "Fuente de Poder EVGA 750W Gold", Precio = 105.00m, Stock = 14, ImagenUrl = "https://images.unsplash.com/photo-1616877562265-d4ffd9d6f47f?q=80&w=1169&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D" },
    new Producto { Id = 11, Nombre = "Gabinete NZXT H5 Flow", Precio = 94.99m, Stock = 7, ImagenUrl = "https://images.unsplash.com/photo-1587202372775-e229f172b9d7?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 12, Nombre = "Refrigeración Líquida Kraken 240", Precio = 139.99m, Stock = 21, ImagenUrl = "https://images.unsplash.com/photo-1613140505986-c64dd5894d97?q=80&w=764&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D" },
    new Producto { Id = 13, Nombre = "Silla Gamer Secretlab Titan", Precio = 450.00m, Stock = 4, ImagenUrl = "https://images.unsplash.com/photo-1671063125699-2c25dac5d1c5?q=80&w=687&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D" },
    new Producto { Id = 14, Nombre = "Micrófono Shure MV7 USB", Precio = 249.00m, Stock = 9, ImagenUrl = "https://images.unsplash.com/photo-1590602847861-f357a9332bbc?w=500&auto=format&fit=crop&q=60" },
    new Producto { Id = 15, Nombre = "Cámara Web Logitech C920 HD", Precio = 79.99m, Stock = 22, ImagenUrl = "https://images.unsplash.com/photo-1626581795188-8efb9a00eeec?q=80&w=735&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D" },
    new Producto { Id = 16, Nombre = "Pad Mouse Razer Strider XXL", Precio = 49.99m, Stock = 40, ImagenUrl = "https://assets2.razerzone.com/images/pnx.assets/2a3cd3ef9fdef900c6b3fff960863f41/razer-strider-stitched-500x500.jpg" },
    new Producto { Id = 17, Nombre = "Sintonizador Elgato Stream Deck MK2", Precio = 149.99m, Stock = 11, ImagenUrl = "https://media.ldlc.com/bo/images/fiches/carte%20acquisition/Elgato/Stream_Deck_MK2/1.jpg" },
    new Producto { Id = 18, Nombre = "Router ASUS ROG Rapture Wi-Fi 6", Precio = 299.00m, Stock = 6, ImagenUrl = "https://cdn.mos.cms.futurecdn.net/qJNfzspoxuQc54HM8hB9wG.jpg" },
    new Producto { Id = 19, Nombre = "Consola Nintendo Switch OLED", Precio = 349.99m, Stock = 3, ImagenUrl = "https://images.unsplash.com/photo-1715081406784-852ba61e4158?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D" },
    new Producto { Id = 20, Nombre = "Mando Xbox Wireless Carbon Black", Precio = 59.99m, Stock = 35, ImagenUrl = "https://m.media-amazon.com/images/I/615-Ww2D6tL.jpg" }
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