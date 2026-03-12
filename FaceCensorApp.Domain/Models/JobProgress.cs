namespace FaceCensorApp.Domain.Models;

public sealed record JobProgress(
    int CurrentIndex,
    int TotalItems,
    string CurrentItem,
    string StatusText)
{
    public double Percentage =>
        TotalItems <= 0 ? 0d : (double)CurrentIndex / TotalItems * 100d;
}