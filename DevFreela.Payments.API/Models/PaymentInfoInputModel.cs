namespace DevFreela.Payments.API.Models
{
    public class PaymentInfoInputModel
    {
        public int IdProject { get; set; }
        public string CreditCardNumber { get; set; }
        public string CVV { get; set; }
        public string ExpiresAt { get; set; }
        public string FullName { get; set; }
        public decimal Amount { get; set; }
    }
}
