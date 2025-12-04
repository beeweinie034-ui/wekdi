using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using wekdi.Models;
using System.Linq;
using System.Threading.Tasks;

namespace wekdi.Controllers
{
    public class CheckoutController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStripeSession(PaymentViewModel model)
        {
            decimal subtotal = model.CartItems.Sum(c => c.Product!.Price * c.Quantity);
            decimal tax = subtotal * model.TaxRate;
            decimal total = subtotal + tax;

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                SuccessUrl = Url.Action("StripeSuccess", "Checkout", null, Request.Scheme),
                CancelUrl = Url.Action("StripeCancel", "Checkout", null, Request.Scheme),
                LineItems = model.CartItems.Select(c => new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmountDecimal = c.Product!.Price * 100, // Stripe expects cents
                        Currency = "myr",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = c.Product.Name
                        }
                    },
                    Quantity = c.Quantity
                }).ToList()
            };

            var service = new SessionService();
            Session session = await service.CreateAsync(options);

            return Redirect(session.Url);
        }

        public IActionResult StripeSuccess() => View();
        public IActionResult StripeCancel() => View();
    }
}
