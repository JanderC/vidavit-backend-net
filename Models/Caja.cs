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
        public string Categoria { get; set; }
        public decimal Monto { get; set; }
        public string Descripcion { get; set; }
        public Guid? ReferenciaId { get; set; }
        public Guid UsuarioId { get; set; }
        public DateTime Fecha { get; set; }
        public string MetodoPago { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navegación
        public Usuario Usuario { get; set; }
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
}