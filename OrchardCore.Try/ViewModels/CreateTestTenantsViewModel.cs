using System.ComponentModel.DataAnnotations;

namespace OrchardCore.Try.ViewModels;

public class CreateTestTenantsViewModel
{
    [Range(1, 1000)]
    public int Count { get; set; } = 100;
}
