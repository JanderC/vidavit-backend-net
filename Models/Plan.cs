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
        public string? Descripcion { get; set; }

        [Column("tipo")]
        [Required]
        [MaxLength(20)]
        public string Tipo { get; set; }

        [Column("duracion_dias")]
        [Required]
        public int DuracionDias { get; set; }

        // 🆕 NUEVO CAMPO
        /// <summary>
        /// Tipo de cálculo de vencimiento:
        /// - "dias": Suma días corridos (ejemplo: 30 días desde inicio)
        /// - "meses": Suma meses calendario (ejemplo: mismo día del mes siguiente)
        /// - "semanas": Suma semanas (7 días * número de semanas)
        /// - "anios": Suma años calendario
        /// </summary>
        [Column("tipo_calculo_vencimiento")]
        [MaxLength(20)]
        public string? TipoCalculoVencimiento { get; set; } = "dias";

        // 🆕 NUEVO CAMPO
        /// <summary>
        /// Cantidad de unidades a sumar según el tipo de cálculo.
        /// Ejemplo: Si TipoCalculoVencimiento = "meses" y CantidadUnidades = 1, suma 1 mes.
        /// Si TipoCalculoVencimiento = "dias" y CantidadUnidades = 30, suma 30 días.
        /// </summary>
        [Column("cantidad_unidades")]
        public int CantidadUnidades { get; set; } = 1;

        [Column("precio")]
        [Required]
        public decimal Precio { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("color")]
        [MaxLength(7)]
        public string? Color { get; set; } = "#10b981";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}