using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidaFitBackend.Models
{
    [Table("checkins")]
    public class CheckIn
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("cliente_id")]
        [Required]
        public Guid ClienteId { get; set; }

        [Column("fecha_hora")]
        public DateTime FechaHora { get; set; } = DateTime.Now;

        [Column("metodo")]
        [MaxLength(20)]
        public string Metodo { get; set; } = "huella";

        [Column("exitoso")]
        public bool Exitoso { get; set; } = true;

        [Column("nota")]
        public string Nota { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Propiedades de Navegación
        [ForeignKey("ClienteId")]
        public Cliente Cliente { get; set; }
    }
}