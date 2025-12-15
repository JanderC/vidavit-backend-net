using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidaFitBackend.Models
{
    [Table("planes")]
    public class Plan
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("nombre")]
        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; }

        [Column("descripcion")]
        public string Descripcion { get; set; }

        [Column("tipo")]
        [Required]
        [MaxLength(20)]
        public string Tipo { get; set; }

        [Column("duracion_dias")]
        [Required]
        public int DuracionDias { get; set; }

        [Column("precio")]
        [Required]
        public decimal Precio { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("color")]
        [MaxLength(7)]
        public string Color { get; set; } = "#00FF00";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}