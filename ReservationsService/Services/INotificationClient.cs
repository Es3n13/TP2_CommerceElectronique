using System.Threading.Tasks;

namespace ReservationsService.Services
{
    public interface INotificationClient
    {
        Task SendConfirmationAsync(int userId, int reservationId);
    }
}
