using System;
using System.Collections.Generic;

namespace VidaFitBackend.Models
{
    /// <summary>
    /// Movimientos de caja (ingresos y egresos)
    /// </summary>
    public class MovimientoCaja
    {
        public Guid Id { get; set; }
        public string Tipo { get; set; } // ingreso/egreso
        public string? Categoria { get; set; }
        public decimal Monto { get; set; }
        public string Descripcion { get; set; }
        public Guid? ReferenciaId { get; set; }
        public Guid UsuarioId { get; set; }
        public DateTime Fecha { get; set; }
        public string MetodoPago { get; set; }
        public bool Cerrado { get; set; } // Indica si el movimiento ya fue cerrado
        public Guid? CierreCajaId { get; set; } // Referencia al cierre que incluyó este movimiento
        public DateTime CreatedAt { get; set; }

        // Navegación
        public Usuario Usuario { get; set; }
        public CierreCaja CierreCaja { get; set; }
    }

    /// <summary>
    /// Empleados del gimnasio
    /// </summary>
    public class Empleado
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Cedula { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
        public string Cargo { get; set; }
        public decimal? Salario { get; set; }
        public DateTime? FechaContratacion { get; set; }
        public bool Activo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Pagos de salarios a empleados
    /// </summary>
    public class PagoEmpleado
    {
        public Guid Id { get; set; }
        public Guid EmpleadoId { get; set; }
        public decimal Monto { get; set; }
        public string Periodo { get; set; }
        public DateTime FechaPago { get; set; }
        public string MetodoPago { get; set; }
        public string Notas { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navegación
        public Empleado Empleado { get; set; }
    }

    /// <summary>
    /// Deudas de clientes
    /// </summary>
    public class DeudaCliente
    {
        public Guid Id { get; set; }
        public Guid ClienteId { get; set; }
        public string Concepto { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal MontoPagado { get; set; }
        public decimal Saldo { get; set; }
        public string Estado { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public string Notas { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navegación
        public Cliente Cliente { get; set; }
        public List<AbonoDeuda> Abonos { get; set; }
    }

    /// <summary>
    /// Abonos realizados a deudas
    /// </summary>
    public class AbonoDeuda
    {
        public Guid Id { get; set; }
        public Guid DeudaId { get; set; }
        public decimal Monto { get; set; }
        public string MetodoPago { get; set; }
        public DateTime FechaAbono { get; set; }
        public string Notas { get; set; }

        public DeudaCliente Deuda { get; set; }
    }

    /// <summary>
    /// Caja Fuerte - Balance principal del negocio
    /// </summary>
    public class CajaFuerte
    {
        public Guid Id { get; set; }
        public decimal BalanceEfectivo { get; set; } // Dinero físico real
        public decimal BalanceTransferencias { get; set; } // Registro contable de transferencias
        public decimal BalanceTotal { get; set; } // Suma de ambos
        public DateTime UltimaActualizacion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Movimientos de la Caja Fuerte
    /// </summary>
    public class MovimientoCajaFuerte
    {
        public Guid Id { get; set; }
        public string Tipo { get; set; } // ingreso/egreso
        public string Origen { get; set; } // cierre_caja/retiro_manual/transferencia_a_caja/transferencia_desde_caja/otros
        public string MetodoPago { get; set; } // efectivo/transferencia
        public decimal Monto { get; set; }
        public string Descripcion { get; set; }
        public string? Categoria { get; set; } // Para egresos: pago_proveedores, servicios_publicos, mantenimiento, nomina, otros
        public Guid? CierreCajaId { get; set; } // Si viene de un cierre
        public Guid? MovimientoCajaId { get; set; } // Referencia al movimiento original de caja diaria
        public Guid UsuarioId { get; set; }
        public DateTime Fecha { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navegación
        public Usuario Usuario { get; set; }
        public CierreCaja CierreCaja { get; set; }
        public MovimientoCaja MovimientoCaja { get; set; }
    }

    /// <summary>
    /// Consolidados mensuales - Resúmenes históricos
    /// </summary>
    public class ConsolidadoMensual
    {
        public Guid Id { get; set; }
        public int Mes { get; set; } // 1-12
        public int Anio { get; set; } // 2025, 2026, etc
        public decimal TotalIngresosEfectivo { get; set; }
        public decimal TotalIngresosTransferencia { get; set; }
        public decimal TotalEgresosEfectivo { get; set; }
        public decimal TotalEgresosTransferencia { get; set; }
        public decimal BalanceFinalEfectivo { get; set; }
        public decimal BalanceFinalTransferencia { get; set; }
        public DateTime FechaConsolidacion { get; set; } // Cuándo se consolidó
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Configuración de Caja Fuerte
    /// </summary>
    public class ConfiguracionCajaFuerte
    {
        public Guid Id { get; set; }
        public string PasswordHash { get; set; } // Contraseña encriptada
        public bool RequiereCambioPassword { get; set; } // True si es primera vez (123456)
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Registro de movimientos eliminados de Caja Fuerte
    /// </summary>
    public class MovimientoEliminado
    {
        public Guid Id { get; set; }
        public Guid MovimientoOriginalId { get; set; }
        public string Tipo { get; set; } // ingreso/egreso
        public string Origen { get; set; }
        public string MetodoPago { get; set; }
        public decimal Monto { get; set; }
        public string Descripcion { get; set; }
        public string Categoria { get; set; }
        public DateTime FechaOriginal { get; set; }
        public DateTime FechaEliminacion { get; set; }
        public Guid UsuarioEliminacion { get; set; }
        public string MotivoEliminacion { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navegación
        public Usuario Usuario { get; set; }
    }
}