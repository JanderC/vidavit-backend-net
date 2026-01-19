using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidaFitBackend.Models
{
    [Table("clientes")]
    public class Cliente
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("nombre")]
        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; }

        [Column("apellido")]
        [Required]
        [MaxLength(100)]
        public string Apellido { get; set; }

        [Column("cedula")]
        [Required]
        [MaxLength(20)]
        public string Cedula { get; set; }

        [Column("telefono")]
        [MaxLength(20)]
        public string? Telefono { get; set; }

        [Column("email")]
        [MaxLength(150)]
        public string? Email { get; set; }

        [Column("fecha_nacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [Column("direccion")]
        public string? Direccion { get; set; }

        [Column("peso")]
        public decimal? Peso { get; set; }

        [Column("huella_digital")]
        public string? HuellaDigital { get; set; }

        [Column("huella_template")]
        public string? HuellaTemplate { get; set; }

        [Column("foto_base64")]
        public string? FotoBase64 { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Membresia> Membresias { get; set; }
        public ICollection<CheckIn> CheckIns { get; set; }
    }
}