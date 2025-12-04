
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Stripe.Checkout;
using wekdi.Data;
using wekdi.Models;



[Authorize(Roles = "Member")]
public class ShoppingCartController : Controller
{
    private readonly ApplicationDbContext _db;
    public ShoppingCartController(ApplicationDbContext db)
    {
        _db = db;

        // Set QuestPDF license type here
        QuestPDF.Settings.License = LicenseType.Community;
    }


    // ---------- AJAX add to cart ----------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddAjax([FromBody] AddAjaxRequest req)
    {
        int memberId = GetCurrentMemberId();

        // Count DISTINCT products
        int distinctProducts = _db.Carts.Count(c => c.UserId == memberId);

        var cartItem = _db.Carts
            .FirstOrDefault(c => c.UserId == memberId && c.ProductId == req.ProductId);

        if (cartItem == null)
        {
            if (distinctProducts >= 10)
            {
                return Json(new
                {
                    success = false,
                    message = "Cart is full – maximum of 10 different products."
                });
            }

            int qty = Math.Clamp(req.Quantity, 1, 10);
            cartItem = new Cart
            {
                ProductId = req.ProductId,
                UserId = memberId,
                Quantity = qty
            };
            _db.Carts.Add(cartItem);
        }
        else
        {
            if (cartItem.Quantity >= 10)
            {
                return Json(new
                {
                    success = false,
                    message = "You can only have up to 10 of this product."
                });
            }

            cartItem.Quantity = Math.Min(cartItem.Quantity + req.Quantity, 10);
        }

        _db.SaveChanges();

        int newDistinct = _db.Carts.Count(c => c.UserId == memberId);
        return Json(new
        {
            success = true,
            totalProducts = newDistinct,
            currentQty = cartItem.Quantity,
            message = "Item successfully added to cart!"
        });
    }

    [HttpPost]
    public IActionResult AddToCart(int productId, int quantity)
    {
        if (quantity < 1) quantity = 1;
        if (quantity > 10) quantity = 10;

        int memberId = GetCurrentMemberId();
        int distinctCount = _db.Carts.Count(c => c.UserId == memberId);

        var existing = _db.Carts
            .FirstOrDefault(c => c.UserId == memberId && c.ProductId == productId);

        if (existing != null)
        {
            if (existing.Quantity + quantity > 10)
            {
                TempData["CartError"] = "You can only have up to 10 of this product.";
                return RedirectToAction("Index");
            }
            existing.Quantity += quantity;
        }
        else
        {
            if (distinctCount >= 10)
            {
                TempData["CartError"] = "Cart is full – maximum of 10 different products.";
                return RedirectToAction("Index");
            }

            _db.Carts.Add(new Cart
            {
                UserId = memberId,
                ProductId = productId,
                Quantity = quantity
            });
        }

        _db.SaveChanges();
        TempData["CartMessage"] = "Item successfully added to cart!";
        return RedirectToAction("Index");
    }

    public class AddAjaxRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    // ---------- Show cart ----------
    public async Task<IActionResult>
    Index()
    {
        int memberId = GetCurrentMemberId();

        var items = await _db.Carts
        .Include(c => c.Product)
        .Where(c => c.UserId == memberId)
        .ToListAsync();

        ViewBag.Total = items.Sum(i => i.Product!.Price * i.Quantity);
        ViewBag.TotalQuantity = items.Sum(i => i.Quantity);
        ViewBag.TotalProducts = items.Count;

        return View(items);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateQuantity(int cartId, int quantity)
    {
        quantity = Math.Clamp(quantity, 1, 10);

        int memberId = GetCurrentMemberId();
        var cartItem = _db.Carts
        .FirstOrDefault(c => c.CartId == cartId && c.UserId == memberId);

        if (cartItem != null)
        {
            cartItem.Quantity = quantity;
            _db.SaveChanges();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        Remove(int cartId)
    {
        int memberId = GetCurrentMemberId();
        var item = await _db.Carts
        .FirstOrDefaultAsync(c => c.CartId == cartId && c.UserId == memberId);

        if (item != null)
        {
            _db.Carts.Remove(item);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------
    private int GetCurrentMemberId()
    {
        var username = User.Identity!.Name!;
        var member = _db.Members.First(m => m.Username == username);
        return member.MemberId;
    }

    [HttpGet]
    public async Task<IActionResult>
        Checkout()
    {
        int memberId = GetCurrentMemberId();

        var items = await _db.Carts
        .Include(c => c.Product)
        .Where(c => c.UserId == memberId)
        .ToListAsync();

        ViewBag.Total = items.Sum(i => i.Product!.Price * i.Quantity);
        ViewBag.TotalQuantity = items.Sum(i => i.Quantity);
        ViewBag.TotalProducts = items.Count;

        return View(items);   // pass cart items as model
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ProcessCheckout(string orderType, string? serviceMethod, int? tableNumber)
    {
        if (orderType == "DineIn")
        {
            // Only validate table number if Table Service
            if (serviceMethod == "TableService")
            {
                if (!tableNumber.HasValue || tableNumber.Value < 1 || tableNumber.Value > 250)
                {
                    ModelState.AddModelError("", "Please enter a valid table number (1–250).");
                    return View("Checkout");
                }
            }

            TempData["OrderType"] = "Dine In";
            TempData["ServiceMethod"] = serviceMethod;
            TempData["TableNumber"] = tableNumber; // null if counter
            TempData["TaxRate"] = "0.10";
        }
        else // Takeaway
        {
            TempData["OrderType"] = "Takeaway";
            TempData["ServiceMethod"] = null;
            TempData["TableNumber"] = null;
            TempData["TaxRate"] = "0";
        }

        return RedirectToAction("Payment");
    }



    [HttpGet]
    public IActionResult Payment()
    {
        int memberId = GetCurrentMemberId();
        var cartItems = _db.Carts.Include(c => c.Product).Where(c => c.UserId == memberId).ToList();

        decimal subtotal = cartItems.Sum(c => c.Product!.Price * c.Quantity);

        // Read TempData for order info
        string orderType = TempData["OrderType"]?.ToString() ?? "Takeaway";
        string serviceMethod = TempData["ServiceMethod"]?.ToString();
        int? tableNumber = TempData["TableNumber"] as int?;
        decimal taxRate = decimal.TryParse(TempData["TaxRate"]?.ToString(), out var tr) ? tr : 0m;
        decimal tax = subtotal * taxRate;
        decimal grandTotal = subtotal + tax;

        var model = new PaymentViewModel
        {
            CartItems = cartItems,
            OrderType = orderType,
            TableNumber = tableNumber,
            TaxRate = taxRate
        };

        ViewBag.Subtotal = subtotal;
        ViewBag.Tax = tax;
        ViewBag.GrandTotal = grandTotal;

        return View(model);
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ProcessPayment(PaymentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        int memberId = GetCurrentMemberId();

        // Load cart items with products
        var cartItems = _db.Carts
            .Include(c => c.Product)
            .Where(c => c.UserId == memberId)
            .ToList();

        if (!cartItems.Any())
        {
            TempData["CartError"] = "Your cart is empty.";
            return RedirectToAction("Index");
        }

        // --- Calculate totals ---
        decimal subtotal = cartItems.Sum(c => c.Product!.Price * c.Quantity);
        decimal tax = subtotal * model.TaxRate;
        decimal grandTotal = subtotal + tax;

        // --- Create Order record ---
        var order = new Order
        {
            MemberId = memberId,
            OrderDate = DateTime.Now,
            Items = string.Join(", ",
                     cartItems.Select(c => $"{c.Product!.Name} x{c.Quantity}")),
            TotalAmount = grandTotal,
            Status = "Successful",
            Notes = $"OrderType: {model.OrderType}, Table: {model.TableNumber}"
        };

        _db.Orders.Add(order);
        _db.SaveChanges();                 // ensures we get the OrderId

        // --- Create OrderItem rows ---
        foreach (var item in cartItems)
        {
            var orderItem = new OrderItem
            {
                OrderId = order.OrderId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                Price = item.Product!.Price
            };
            _db.OrderItems.Add(orderItem);
        }
        _db.SaveChanges();

        // --- Optional: generate PDF & email receipt (if you want to keep it) ---
        var pdfBytes = GeneratePdf(cartItems, subtotal, tax, grandTotal);
        

        // --- Clear the cart ---
        _db.Carts.RemoveRange(cartItems);
        _db.SaveChanges();

        return RedirectToAction("Confirmation", new { id = order.OrderId });
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DownloadReceipt(PaymentViewModel model)
    {
        int memberId = GetCurrentMemberId();

        var cartItems = _db.Carts
            .Include(c => c.Product)
            .Where(c => c.UserId == memberId && c.Product != null)
            .ToList();

        if (!cartItems.Any())
            return RedirectToAction("Index");

        decimal subtotal = cartItems.Sum(c => c.Product!.Price * c.Quantity);
        decimal tax = subtotal * model.TaxRate;
        decimal grandTotal = subtotal + tax;

        var pdfBytes = GeneratePdf(cartItems, subtotal, tax, grandTotal);

        return File(pdfBytes, "application/pdf", "E-Receipt.pdf");
    }



    private void SendReceiptEmail(string toEmail, byte[] pdfBytes)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Mekdi", "wolfieloverssthemaster.com")); // replace
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Your E-Receipt";

        var body = new BodyBuilder();
        body.TextBody = "Thank you for your order! Please find your receipt attached.";
        body.Attachments.Add("E-Receipt.pdf", pdfBytes, new ContentType("application", "pdf"));

        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        client.Connect("smtp.yourmailserver.com", 587, MailKit.Security.SecureSocketOptions.StartTls); // replace
        client.Authenticate("yourusername", "yourpassword"); // replace
        client.Send(message);
        client.Disconnect(true);
    }


    private byte[] GeneratePdf(IEnumerable<Cart>
        cartItems, decimal subtotal, decimal tax, decimal grandTotal)
    {
        // Using QuestPDF
        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Content().Column(column =>
                {
                    column.Item().Text("E-Receipt").FontSize(20).Bold().Underline();
                    column.Item().Text("------------------------------");

                    foreach (var item in cartItems)
                    {
                        decimal total = item.Quantity * item.Product!.Price;
                        column.Item().Text($"{item.Product.Name} x{item.Quantity} - {item.Product.Price:C} = {total:C}");
                    }

                    column.Item().Text("------------------------------");
                    column.Item().Text($"Subtotal: {subtotal:C}");
                    column.Item().Text($"Tax: {tax:C}");
                    column.Item().Text($"Grand Total: {grandTotal:C}");
                });
            });
        }).GeneratePdf();

        return pdfBytes;
    }

    public IActionResult Confirmation(int id)
    {
        var order = _db.Orders.Include(o => o.Member)
                              .FirstOrDefault(o => o.OrderId == id);
        if (order == null) return RedirectToAction("Index");
        return View(order);
    }










}
