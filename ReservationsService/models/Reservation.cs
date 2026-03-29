namespace ReservationsService.Models
{
    public class Reservation
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ResourceId { get; set; }
        public DateTime Date { get; set; }
    }
}
