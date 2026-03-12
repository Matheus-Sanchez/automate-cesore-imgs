using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Application.Contracts;

public interface ISettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}