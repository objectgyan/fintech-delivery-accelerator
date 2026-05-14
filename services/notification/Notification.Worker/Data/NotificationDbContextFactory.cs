using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notification.Worker.Data;

internal sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlServer("Server=.;Database=FinTechNotification;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        return new NotificationDbContext(options);
    }
}
