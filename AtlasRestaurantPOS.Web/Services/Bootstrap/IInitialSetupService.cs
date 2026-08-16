namespace AtlasRestaurantPOS.Web.Services.Bootstrap;

public interface IInitialSetupService
{
    Task<InitialSetupResult> ExecuteInitialSetupAsync();
}