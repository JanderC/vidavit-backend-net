using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidaFitBackend.Models
{
    [Table("membresias")]
    public class Membresia
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("cliente_id")]
        [Required]
        public Guid ClienteId { get; set; }

        [Column("plan_id")]
        [Required]
        public Guid PlanId { get; set; }

        [Column("fecha_inicio")]
        [Required]
        public DateTime FechaInicio { get; set; }

        [Column("fecha_vencimiento")]
        [Required]
        public DateTime FechaVencimiento { get; set; }

        [Column("estado")]
        [MaxLength(20)]
        public string Estado { get; set; } = "activa";

        [Column("monto_pagado")]
        [Required]
        public decimal MontoPagado { get; set; }

        [Column("metodo_pago")]
        [MaxLength(50)]
        public string MetodoPago { get; set; }

        [Column("notas")]
        public string? Notas { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey("ClienteId")]
        public Cliente Cliente { get; set; }

        [ForeignKey("PlanId")]
        public Plan Plan { get; set; }
    }
}