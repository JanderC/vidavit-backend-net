using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidaFitBackend.Models
{
    [Table("notificaciones")]
    public class Notificacion
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("tipo")]
        [Required]
        [MaxLength(50)]
        public string Tipo { get; set; }

        [Column("titulo")]
        [Required]
        [MaxLength(200)]
        public string Titulo { get; set; }

        [Column("mensaje")]
        [Required]
        public string Mensaje { get; set; }

        [Column("cliente_id")]
        public Guid? ClienteId { get; set; }

        [Column("leida")]
        public bool Leida { get; set; } = false;

        [Column("prioridad")]
        [MaxLength(20)]
        public string Prioridad { get; set; } = "normal";

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [Column("fecha_leida")]
        public DateTime? FechaLeida { get; set; }

        [ForeignKey("ClienteId")]
        public Cliente Cliente { get; set; }
    }
}