using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidaFitBackend.Models
{
    [Table("pagos")]
    public class Pago
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("membresia_id")]
        [Required]
        public Guid MembresiaId { get; set; }

        [Column("cliente_id")]
        [Required]
        public Guid ClienteId { get; set; }

        [Column("monto")]
        [Required]
        public decimal Monto { get; set; }

        [Column("metodo_pago")]
        [Required]
        [MaxLength(50)]
        public string? MetodoPago { get; set; }

        [Column("fecha_pago")]
        public DateTime FechaPago { get; set; } = DateTime.Now;

        [Column("recibo_numero")]
        [MaxLength(50)]
        public string? ReciboNumero { get; set; }

        [Column("notas")]
        public string? Notas { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Propiedades de Navegación (Relaciones)
        [ForeignKey("MembresiaId")]
        public virtual Membresia? Membresia { get; set; }

        [ForeignKey("ClienteId")]
        public virtual Cliente? Cliente { get; set; }
    }
}