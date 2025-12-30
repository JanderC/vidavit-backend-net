using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidaFitBackend.Models
{
    [Table("ventas_productos")]
    public class VentaProducto
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("cliente_id")]
        [Required]
        public Guid? ClienteId { get; set; }

        [Column("producto_id")]
        [Required]
        public Guid ProductoId { get; set; }

        [Column("cantidad")]
        [Required]
        public int Cantidad { get; set; } = 1;

        [Column("precio_unitario")]
        [Required]
        public decimal PrecioUnitario { get; set; }

        [Column("total")]
        [Required]
        public decimal Total { get; set; }

        [Column("estado_pago")]
        [MaxLength(20)]
        public string EstadoPago { get; set; } = "pendiente";

        [Column("fecha_venta")]
        public DateTime FechaVenta { get; set; } = DateTime.Now;

        [Column("fecha_pago")]
        public DateTime? FechaPago { get; set; }

        [Column("notas")]
        public string? Notas { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey("ClienteId")]
        public Cliente? Cliente { get; set; }

        [ForeignKey("ProductoId")]
        public Producto Producto { get; set; }
    }
}