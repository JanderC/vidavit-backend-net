using System;

namespace VidaFitBackend.Models
{
    /// <summary>
    /// Registro histórico de cierres de caja
    /// </summary>
    public class CierreCaja
    {
        public Guid Id { get; set; }
        public DateTime FechaCierre { get; set; }
        public string TipoCierre { get; set; } // "automatico" o "manual"

        // Montos en efectivo
        public decimal EfectivoInicial { get; set; }
        public decimal IngresosEfectivo { get; set; }
        public decimal EgresosEfectivo { get; set; }
        public decimal EfectivoFinal { get; set; }

        // Montos en transferencia
        public decimal IngresosTransferencia { get; set; }
        public decimal EgresosTransferencia { get; set; }

        // Totales generales
        public decimal TotalIngresos { get; set; }
        public decimal TotalEgresos { get; set; }
        public decimal BalanceGeneral { get; set; }

        // Detalles
        public int CantidadMovimientos { get; set; }
        public string Observaciones { get; set; }
        public Guid UsuarioId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navegación
        public Usuario Usuario { get; set; }
    }
}