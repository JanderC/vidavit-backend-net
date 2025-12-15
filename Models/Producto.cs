using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidaFitBackend.Models
{
    [Table("productos")]
    public class Producto
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("nombre")]
        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; }

        [Column("descripcion")]
        public string Descripcion { get; set; }

        [Column("precio")]
        [Required]
        public decimal Precio { get; set; }

        [Column("stock")]
        public int Stock { get; set; } = 0;

        [Column("imagen_base64")]
        public string? ImagenBase64 { get; set; }

        [Column("categoria")]
        [MaxLength(50)]
        public string Categoria { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}