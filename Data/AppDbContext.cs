using Microsoft.EntityFrameworkCore;
using VidaFitBackend.Models;

namespace VidaFit.Data
{
    /// <summary>
    /// Context de Entity Framework Core para VIDA FIT
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
            });

            modelBuilder.Entity<Membresia>(entity =>
            {
                entity.ToTable("membresias");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MontoPagado).HasColumnType("decimal(10,2)");

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

            // ==================== NUEVAS TABLAS SISTEMA DE CAJA ====================

            modelBuilder.Entity<MovimientoCaja>(entity =>
            {
                entity.ToTable("movimientos_caja");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Monto).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Tipo).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Categoria).IsRequired().HasMaxLength(50);

                // Relación con Usuario
                entity.HasOne(m => m.Usuario)
                    .WithMany()
                    .HasForeignKey(m => m.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índices
                entity.HasIndex(m => m.Fecha);
                entity.HasIndex(m => m.Tipo);
                entity.HasIndex(m => m.Categoria);
            });

            modelBuilder.Entity<Empleado>(entity =>
            {
                entity.ToTable("empleados");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Apellido).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Cedula).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Salario).HasColumnType("decimal(10,2)");
                entity.HasIndex(e => e.Cedula).IsUnique();
                entity.HasIndex(e => e.Activo);
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
                    .OnDelete(DeleteBehavior.Cascade);

                // Índices
                entity.HasIndex(p => p.EmpleadoId);
                entity.HasIndex(p => p.FechaPago);
            });

            modelBuilder.Entity<DeudaCliente>(entity =>
            {
                entity.ToTable("deudas_clientes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Concepto).IsRequired().HasMaxLength(200);
                entity.Property(e => e.MontoTotal).HasColumnType("decimal(10,2)");
                entity.Property(e => e.MontoPagado).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Saldo).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Estado).HasMaxLength(20);

                // Relación
                entity.HasOne(d => d.Cliente)
                    .WithMany()
                    .HasForeignKey(d => d.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Índices
                entity.HasIndex(d => d.ClienteId);
                entity.HasIndex(d => d.Estado);
                entity.HasIndex(d => d.FechaVencimiento);
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

                // Índices
                entity.HasIndex(a => a.DeudaId);
                entity.HasIndex(a => a.FechaAbono);
            });

            modelBuilder.Entity<CierreCaja>(entity =>
            {
                entity.ToTable("cierres_caja");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EfectivoInicial).HasColumnType("decimal(10,2)");
                entity.Property(e => e.IngresosEfectivo).HasColumnType("decimal(10,2)");
                entity.Property(e => e.EgresosEfectivo).HasColumnType("decimal(10,2)");
                entity.Property(e => e.EfectivoFinal).HasColumnType("decimal(10,2)");
                entity.Property(e => e.IngresosTransferencia).HasColumnType("decimal(10,2)");
                entity.Property(e => e.EgresosTransferencia).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TotalIngresos).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TotalEgresos).HasColumnType("decimal(10,2)");
                entity.Property(e => e.BalanceGeneral).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TipoCierre).IsRequired().HasMaxLength(20);

                // Relación con Usuario
                entity.HasOne(c => c.Usuario)
                    .WithMany()
                    .HasForeignKey(c => c.UsuarioId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Índices
                entity.HasIndex(c => c.FechaCierre);
                entity.HasIndex(c => c.TipoCierre);
            });

            // ==================== CONFIGURACIÓN DE CONVERSIÓN DE NOMBRES ====================

            // Configurar nombres de columnas en snake_case (PostgreSQL style)
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    // Convertir propiedades a snake_case
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
                        // Nuevas propiedades para sistema de caja
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
                        // Propiedades para CierreCaja
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
                        _ => property.Name.ToLower()
                    };

                    property.SetColumnName(columnName);
                }
            }
        }

        /// <summary>
        /// Sobrescribe SaveChanges para actualizar automáticamente UpdatedAt
        /// </summary>
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        /// <summary>
        /// Sobrescribe SaveChangesAsync para actualizar automáticamente UpdatedAt
        /// </summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Actualiza automáticamente CreatedAt y UpdatedAt
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
                            createdAtProp.CurrentValue = DateTime.UtcNow;
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
                        updatedAtProp.CurrentValue = DateTime.UtcNow;
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