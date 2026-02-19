using Microsoft.EntityFrameworkCore;
using VidaFitBackend.Models;

namespace VidaFit.Data
{
    /// <summary>
    /// Context de Entity Framework Core para VIDA FIT
    /// CONFIGURADO PARA USAR HORA LOCAL DEL SERVIDOR
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSets - Tablas de la base de datos
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Plan> Planes { get; set; }
        public DbSet<Membresia> Membresias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<VentaProducto> VentasProductos { get; set; }
        public DbSet<CheckIn> CheckIns { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<Notificacion> Notificaciones { get; set; }

        // Nuevas tablas para sistema de caja
        public DbSet<MovimientoCaja> MovimientosCaja { get; set; }
        public DbSet<Empleado> Empleados { get; set; }
        public DbSet<PagoEmpleado> PagosEmpleados { get; set; }
        public DbSet<DeudaCliente> DeudasClientes { get; set; }
        public DbSet<AbonoDeuda> AbonosDeuda { get; set; }
        public DbSet<CierreCaja> CierresCaja { get; set; }

        // Nuevas tablas para Caja Fuerte
        public DbSet<CajaFuerte> CajasFuertes { get; set; }
        public DbSet<MovimientoCajaFuerte> MovimientosCajaFuerte { get; set; }
        public DbSet<ConsolidadoMensual> ConsolidadosMensuales { get; set; }
        public DbSet<ConfiguracionCajaFuerte> ConfiguracionesCajaFuerte { get; set; }
        // TEMPORAL: Comentado hasta ejecutar migración
        public DbSet<MovimientoEliminado> MovimientosEliminados { get; set; }

        // ✅ No necesitamos OnConfiguring adicional
        // La configuración de la conexión se hace en Program.cs con EnableLegacyTimestampBehavior

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==================== CONFIGURACIÓN DE TABLAS ====================

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.ToTable("usuarios");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.ToTable("clientes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Apellido).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Cedula).IsRequired().HasMaxLength(20);
                entity.HasIndex(e => e.Cedula).IsUnique();
            });

            modelBuilder.Entity<Plan>(entity =>
            {
                entity.ToTable("planes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Precio).HasColumnType("decimal(10,2)");

                entity.Property(e => e.TipoCalculoVencimiento)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("dias");

                entity.Property(e => e.CantidadUnidades)
                    .IsRequired()
                    .HasDefaultValue(1);
            });

            modelBuilder.Entity<Membresia>(entity =>
            {
                entity.ToTable("membresias");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MontoPagado).HasColumnType("decimal(10,2)");

                // ✅ CONFIGURACIÓN CRÍTICA DE FECHAS
                entity.Property(e => e.FechaInicio).HasColumnType("date");
                entity.Property(e => e.FechaVencimiento).HasColumnType("date");
                entity.Property(e => e.CreatedAt).HasColumnType("date");
                entity.Property(e => e.UpdatedAt).HasColumnType("date");

                // Relaciones
                entity.HasOne(m => m.Cliente)
                    .WithMany(c => c.Membresias)
                    .HasForeignKey(m => m.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.Plan)
                    .WithMany()
                    .HasForeignKey(m => m.PlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Producto>(entity =>
            {
                entity.ToTable("productos");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Precio).HasColumnType("decimal(10,2)");
            });

            modelBuilder.Entity<VentaProducto>(entity =>
            {
                entity.ToTable("ventas_productos");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Total).HasColumnType("decimal(10,2)");

                // Relaciones
                entity.HasOne(v => v.Cliente)
                    .WithMany()
                    .HasForeignKey(v => v.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(v => v.Producto)
                    .WithMany()
                    .HasForeignKey(v => v.ProductoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CheckIn>(entity =>
            {
                entity.ToTable("checkins");
                entity.HasKey(e => e.Id);

                // Relación
                entity.HasOne(c => c.Cliente)
                    .WithMany(cl => cl.CheckIns)
                    .HasForeignKey(c => c.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Índices para optimización
                entity.HasIndex(c => c.FechaHora);
                entity.HasIndex(c => c.ClienteId);
            });

            modelBuilder.Entity<Pago>(entity =>
            {
                entity.ToTable("pagos");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Monto).HasColumnType("decimal(10,2)");

                // Relaciones
                entity.HasOne(p => p.Membresia)
                    .WithMany()
                    .HasForeignKey(p => p.MembresiaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Notificacion>(entity =>
            {
                entity.ToTable("notificaciones");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Titulo).IsRequired().HasMaxLength(200);

                // Relación
                entity.HasOne(n => n.Cliente)
                    .WithMany()
                    .HasForeignKey(n => n.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Índices
                entity.HasIndex(n => n.Leida);
                entity.HasIndex(n => n.FechaCreacion);
            });

            // ==================== CIERRES DE CAJA (PRIMERO) ====================
            modelBuilder.Entity<CierreCaja>(entity =>
            {
                entity.ToTable("cierres_caja");
                entity.HasKey(e => e.Id);

                // Configurar todos los campos decimales
                entity.Property(e => e.EfectivoInicial).HasColumnType("decimal(10,2)");
                entity.Property(e => e.IngresosEfectivo).HasColumnType("decimal(10,2)");
                entity.Property(e => e.EgresosEfectivo).HasColumnType("decimal(10,2)");
                entity.Property(e => e.EfectivoFinal).HasColumnType("decimal(10,2)");
                entity.Property(e => e.IngresosTransferencia).HasColumnType("decimal(10,2)");
                entity.Property(e => e.EgresosTransferencia).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TotalIngresos).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TotalEgresos).HasColumnType("decimal(10,2)");
                entity.Property(e => e.BalanceGeneral).HasColumnType("decimal(10,2)");

                // ✅ CRÍTICO: Configurar relación con Usuario SIN crear columnas extras
                entity.HasOne(c => c.Usuario)
                    .WithMany()
                    .HasForeignKey(c => c.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ✅ CRÍTICO: Ignorar la propiedad de navegación Movimientos
                // La relación inversa se configura en MovimientoCaja
                entity.Ignore(c => c.Movimientos);

                // Índices
                entity.HasIndex(c => c.FechaCierre);
                entity.HasIndex(c => c.UsuarioId);
            });

            // ==================== MOVIMIENTOS DE CAJA (DESPUÉS) ====================
            modelBuilder.Entity<MovimientoCaja>(entity =>
            {
                entity.ToTable("movimientos_caja");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Monto).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Tipo).IsRequired().HasMaxLength(20);

                // ✅ IMPORTANTE: Fecha debe ser TIMESTAMP para guardar hora exacta
                // NO usamos 'date' porque necesitamos saber la hora del movimiento
                entity.Property(e => e.Fecha).HasColumnType("timestamp");

                // CreatedAt puede ser date (solo auditoría del día)
                entity.Property(e => e.CreatedAt).HasColumnType("date");

                // ✅ CRÍTICO: Configurar relación con Usuario
                entity.HasOne(m => m.Usuario)
                    .WithMany()
                    .HasForeignKey(m => m.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ✅ CRÍTICO: Configurar relación con CierreCaja
                entity.HasOne(m => m.CierreCaja)
                    .WithMany()
                    .HasForeignKey(m => m.CierreCajaId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Índices
                entity.HasIndex(m => m.Fecha);
                entity.HasIndex(m => m.Tipo);
                entity.HasIndex(m => m.Cerrado);
                entity.HasIndex(m => m.CierreCajaId);
            });

            // ==================== OTRAS TABLAS ====================

            modelBuilder.Entity<Empleado>(entity =>
            {
                entity.ToTable("empleados");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Apellido).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Cedula).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Salario).HasColumnType("decimal(10,2)");
                entity.HasIndex(e => e.Cedula).IsUnique();
            });

            modelBuilder.Entity<PagoEmpleado>(entity =>
            {
                entity.ToTable("pagos_empleados");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Monto).HasColumnType("decimal(10,2)");

                // Relación
                entity.HasOne(p => p.Empleado)
                    .WithMany()
                    .HasForeignKey(p => p.EmpleadoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DeudaCliente>(entity =>
            {
                entity.ToTable("deudas_clientes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MontoTotal).HasColumnType("decimal(10,2)");
                entity.Property(e => e.MontoPagado).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Saldo).HasColumnType("decimal(10,2)");

                // Relación
                entity.HasOne(d => d.Cliente)
                    .WithMany()
                    .HasForeignKey(d => d.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AbonoDeuda>(entity =>
            {
                entity.ToTable("abonos_deuda");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Monto).HasColumnType("decimal(10,2)");

                // Relación
                entity.HasOne(a => a.Deuda)
                    .WithMany(d => d.Abonos)
                    .HasForeignKey(a => a.DeudaId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==================== CAJA FUERTE ====================

            modelBuilder.Entity<CajaFuerte>(entity =>
            {
                entity.ToTable("caja_fuerte");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.BalanceEfectivo).HasColumnType("decimal(10,2)");
                entity.Property(e => e.BalanceTransferencias).HasColumnType("decimal(10,2)");
                entity.Property(e => e.BalanceTotal).HasColumnType("decimal(10,2)");
            });

            modelBuilder.Entity<MovimientoCajaFuerte>(entity =>
            {
                entity.ToTable("movimientos_caja_fuerte");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Monto).HasColumnType("decimal(10,2)");

                entity.HasOne(m => m.MovimientoCaja)
                    .WithMany()
                    .HasForeignKey(m => m.MovimientoCajaId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(m => m.MovimientoCajaId);
                // ✅ Configurar relación con Usuario
                entity.HasOne(m => m.Usuario)
                    .WithMany()
                    .HasForeignKey(m => m.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ✅ Configurar relación con CierreCaja
                entity.HasOne(m => m.CierreCaja)
                    .WithMany()
                    .HasForeignKey(m => m.CierreCajaId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Índices
                entity.HasIndex(m => m.Fecha);
                entity.HasIndex(m => m.Tipo);
                entity.HasIndex(m => m.UsuarioId);
            });

            modelBuilder.Entity<ConsolidadoMensual>(entity =>
            {
                entity.ToTable("consolidados_mensuales");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.TotalIngresosEfectivo).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TotalIngresosTransferencia).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TotalEgresosEfectivo).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TotalEgresosTransferencia).HasColumnType("decimal(10,2)");
                entity.Property(e => e.BalanceFinalEfectivo).HasColumnType("decimal(10,2)");
                entity.Property(e => e.BalanceFinalTransferencia).HasColumnType("decimal(10,2)");

                entity.HasIndex(e => new { e.Mes, e.Anio }).IsUnique();
                entity.HasIndex(e => e.Anio);
            });

            modelBuilder.Entity<ConfiguracionCajaFuerte>(entity =>
            {
                entity.ToTable("configuracion_caja_fuerte");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            });

            // TEMPORAL: Comentado hasta ejecutar migración
            modelBuilder.Entity<MovimientoEliminado>(entity =>
            {
                entity.ToTable("movimientos_eliminados");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Monto).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Tipo).IsRequired().HasMaxLength(20);
                entity.Property(e => e.MetodoPago).IsRequired().HasMaxLength(50);

                // ✅ Configurar relación con Usuario que eliminó
                entity.HasOne(m => m.Usuario)
                    .WithMany()
                    .HasForeignKey(m => m.UsuarioEliminacion)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índices para búsqueda eficiente
                entity.HasIndex(m => m.FechaEliminacion);
                entity.HasIndex(m => m.FechaOriginal);
                entity.HasIndex(m => m.MovimientoOriginalId);
                entity.HasIndex(m => m.UsuarioEliminacion);
            });


            // ==================== CONVERSIÓN DE NOMBRES A SNAKE_CASE ====================
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    var columnName = property.Name switch
                    {
                        "Id" => "id",
                        "Nombre" => "nombre",
                        "Apellido" => "apellido",
                        "Email" => "email",
                        "PasswordHash" => "password_hash",
                        "Rol" => "rol",
                        "Telefono" => "telefono",
                        "Activo" => "activo",
                        "CreatedAt" => "created_at",
                        "UpdatedAt" => "updated_at",
                        "Cedula" => "cedula",
                        "FechaNacimiento" => "fecha_nacimiento",
                        "Direccion" => "direccion",
                        "HuellaDigital" => "huella_digital",
                        "HuellaTemplate" => "huella_template",
                        "HuellaTemplate1" => "huella_template_1",
                        "HuellaTemplate2" => "huella_template_2",
                        "HuellaTemplate3" => "huella_template_3",
                        "FotoBase64" => "foto_base64",
                        "Descripcion" => "descripcion",
                        "Tipo" => "tipo",
                        "DuracionDias" => "duracion_dias",
                        "Precio" => "precio",
                        "Color" => "color",
                        "TipoCalculoVencimiento" => "tipo_calculo_vencimiento",
                        "CantidadUnidades" => "cantidad_unidades",
                        "ClienteId" => "cliente_id",
                        "PlanId" => "plan_id",
                        "FechaInicio" => "fecha_inicio",
                        "FechaVencimiento" => "fecha_vencimiento",
                        "Estado" => "estado",
                        "MontoPagado" => "monto_pagado",
                        "MetodoPago" => "metodo_pago",
                        "Notas" => "notas",
                        "Stock" => "stock",
                        "ImagenBase64" => "imagen_base64",
                        "Categoria" => "categoria",
                        "ProductoId" => "producto_id",
                        "Cantidad" => "cantidad",
                        "PrecioUnitario" => "precio_unitario",
                        "Total" => "total",
                        "EstadoPago" => "estado_pago",
                        "FechaVenta" => "fecha_venta",
                        "FechaPago" => "fecha_pago",
                        "FechaHora" => "fecha_hora",
                        "Metodo" => "metodo",
                        "Exitoso" => "exitoso",
                        "Nota" => "nota",
                        "MembresiaId" => "membresia_id",
                        "Monto" => "monto",
                        "ReciboNumero" => "recibo_numero",
                        "Titulo" => "titulo",
                        "Mensaje" => "mensaje",
                        "Leida" => "leida",
                        "Prioridad" => "prioridad",
                        "FechaCreacion" => "fecha_creacion",
                        "FechaLeida" => "fecha_leida",
                        "ReferenciaId" => "referencia_id",
                        "UsuarioId" => "usuario_id",
                        "Fecha" => "fecha",
                        "Cargo" => "cargo",
                        "Salario" => "salario",
                        "FechaContratacion" => "fecha_contratacion",
                        "EmpleadoId" => "empleado_id",
                        "Periodo" => "periodo",
                        "Concepto" => "concepto",
                        "MontoTotal" => "monto_total",
                        "Saldo" => "saldo",
                        "DeudaId" => "deuda_id",
                        "FechaAbono" => "fecha_abono",
                        "FechaCierre" => "fecha_cierre",
                        "TipoCierre" => "tipo_cierre",
                        "EfectivoInicial" => "efectivo_inicial",
                        "IngresosEfectivo" => "ingresos_efectivo",
                        "EgresosEfectivo" => "egresos_efectivo",
                        "EfectivoFinal" => "efectivo_final",
                        "IngresosTransferencia" => "ingresos_transferencia",
                        "EgresosTransferencia" => "egresos_transferencia",
                        "TotalIngresos" => "total_ingresos",
                        "TotalEgresos" => "total_egresos",
                        "BalanceGeneral" => "balance_general",
                        "CantidadMovimientos" => "cantidad_movimientos",
                        "Observaciones" => "observaciones",
                        "Cerrado" => "cerrado",
                        "CierreCajaId" => "cierre_caja_id",
                        "BalanceEfectivo" => "balance_efectivo",
                        "BalanceTransferencias" => "balance_transferencias",
                        "BalanceTotal" => "balance_total",
                        "UltimaActualizacion" => "ultima_actualizacion",
                        "Origen" => "origen",
                        "TotalIngresosEfectivo" => "total_ingresos_efectivo",
                        "TotalIngresosTransferencia" => "total_ingresos_transferencia",
                        "TotalEgresosEfectivo" => "total_egresos_efectivo",
                        "TotalEgresosTransferencia" => "total_egresos_transferencia",
                        "BalanceFinalEfectivo" => "balance_final_efectivo",
                        "BalanceFinalTransferencia" => "balance_final_transferencia",
                        "FechaConsolidacion" => "fecha_consolidacion",
                        "Mes" => "mes",
                        "Anio" => "anio",
                        "RequiereCambioPassword" => "requiere_cambio_password",
                        "Peso" => "peso",
                        "MovimientoCajaId" => "movimiento_caja_id",
                        "MovimientoOriginalId" => "movimiento_original_id",
                        "FechaOriginal" => "fecha_original",
                        "FechaEliminacion" => "fecha_eliminacion",
                        "UsuarioEliminacion" => "usuario_eliminacion",
                        "MotivoEliminacion" => "motivo_eliminacion",
                        _ => property.Name.ToLower()
                    };

                    property.SetColumnName(columnName);
                }
            }
        }

        /// <summary>
        /// Sobrescribe SaveChanges para actualizar automáticamente UpdatedAt
        /// ✅ USANDO HORA LOCAL EN LUGAR DE UTC
        /// </summary>
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        /// <summary>
        /// Sobrescribe SaveChangesAsync para actualizar automáticamente UpdatedAt
        /// ✅ USANDO HORA LOCAL EN LUGAR DE UTC
        /// </summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Actualiza automáticamente CreatedAt y UpdatedAt
        /// ✅ USANDO HORA LOCAL DEL SERVIDOR
        /// </summary>
        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    try
                    {
                        var createdAtProp = entry.Property("CreatedAt");
                        if (createdAtProp != null)
                        {
                            // ✅ Usar DateTime.Now (hora local) en lugar de DateTime.UtcNow
                            createdAtProp.CurrentValue = DateTime.Now;
                        }
                    }
                    catch
                    {
                        // Propiedad no existe, continuar
                    }
                }

                try
                {
                    var updatedAtProp = entry.Property("UpdatedAt");
                    if (updatedAtProp != null)
                    {
                        // ✅ Usar DateTime.Now (hora local) en lugar de DateTime.UtcNow
                        updatedAtProp.CurrentValue = DateTime.Now;
                    }
                }
                catch
                {
                    // Propiedad no existe, continuar
                }
            }
        }
    }
}