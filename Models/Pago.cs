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

        // CAMBIO 1: Cambiar el nombre de la propiedad Guid a ClienteId para que sea la Clave Foránea
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
        public virtual Membresia Membresia { get; set; }

        // CAMBIO 2: Agregar la propiedad de navegación del objeto Cliente
        [ForeignKey("ClienteId")]
        public virtual Cliente Cliente { get; set; }
    }
}