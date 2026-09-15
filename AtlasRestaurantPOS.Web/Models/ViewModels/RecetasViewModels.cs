using System.ComponentModel.DataAnnotations;

namespace AtlasRestaurantPOS.Web.Models.ViewModels
{
    public class RecetaForm
    {
        public int IdRecetaProducto { get; set; }

        [Required]
        public int IdProducto { get; set; }

        [Required]
        public int IdInsumo { get; set; }

        [Required]
        [Range(0.000001, double.MaxValue)]
        public decimal Cantidad { get; set; }
    }
}
