using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace A1.Api.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Command> Commands { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<Base> Bases { get; set; }
        public DbSet<Nature> Natures { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<RentalProperty> RentalProperties { get; set; }
        public DbSet<PropertyGroup> PropertyGroups { get; set; }
        public DbSet<RevenueRate> RevenueRates { get; set; }
        public DbSet<FileUpload> FileUploads { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<PropertyGroupLinking> PropertyGroupLinkings { get; set; }
        public DbSet<ContractRiseTerm> ContractRiseTerms { get; set; }
        public DbSet<RentalValueGovtShareRate> RentalValueGovtShareRates { get; set; }
        public DbSet<UserNote> UserNotes { get; set; }
        public DbSet<SharingFormula> SharingFormulas { get; set; }
        public DbSet<BankList> BankLists { get; set; }
        public DbSet<PropertyType> PropertyTypes { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<AccRacBase> AccRacBases { get; set; }

        public DbSet<LockDate> LockDates { get; set; }
        public DbSet<ContractInvoicesEdit> ContractInvoicesEdits { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RentalProperty>().ToTable("RentalProperties", "dbo");
            modelBuilder.Entity<PropertyGroup>().ToTable("PropertyGroups", "dbo");
            modelBuilder.Entity<ContractRiseTerm>().ToTable("ContractRiseTerms", "dbo");
            modelBuilder.Entity<UserNote>().ToTable("UserNotes", "dbo");

            modelBuilder.Entity<AuditLog>(e =>
            {
                e.ToTable("AuditLog", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.EntityName).HasMaxLength(150);
                e.Property(x => x.ActionBy).HasMaxLength(150);
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionDateTime).HasPrecision(3);
            });

            modelBuilder.Entity<UserPermission>(e =>
            {
                e.ToTable("UserPermissions", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.MenuName).HasMaxLength(100).IsRequired();
                e.HasIndex(x => new { x.UserId, x.MenuName }).IsUnique();
            });

            modelBuilder.Entity<AccRacBase>(e =>
            {
                e.ToTable("AccRacBase", "dbo");
                e.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(50);
                e.Property(x => x.Type).HasColumnName("Type").HasMaxLength(10).IsRequired();
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(50);
            });

            modelBuilder.Entity<LockDate>(e =>
            {
                e.ToTable("LockDate", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.LockingDate).HasColumnName("LockingDate").HasColumnType("date");
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(50);
            });

            modelBuilder.Entity<ContractInvoicesEdit>(e =>
            {
                e.ToTable("ContractInvoicesEdit", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.ContractNo).HasMaxLength(50).IsRequired();
                e.Property(x => x.InvoiceNo).HasMaxLength(50).IsRequired();
                e.Property(x => x.PeriodStart).HasColumnType("date");
                e.Property(x => x.PeriodEnd).HasColumnType("date");
                e.Property(x => x.DueDate).HasColumnType("date");
                e.Property(x => x.ContractStartDate).HasColumnType("date");
                e.Property(x => x.ContractEndDate).HasColumnType("date");
                e.Property(x => x.RiseDate).HasColumnType("date");
                e.Property(x => x.CreatedAt).HasColumnType("datetime");
                e.HasIndex(x => new { x.ContractNo, x.InvoiceNo });
            });

            modelBuilder.Entity<BankAccount>(e =>
            {
                e.ToTable("BankAccounts", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.CmdId).HasColumnName("RAC");
                e.Property(x => x.BaseId).HasColumnName("Base");
                e.Property(x => x.OpeningDate).HasColumnType("date");
                e.Property(x => x.SignatoryDate).HasColumnType("date");
                e.Property(x => x.StatusDate).HasColumnType("date");
                e.Property(x => x.IBAN).HasMaxLength(34).IsRequired();
                e.Property(x => x.Signatory1).HasMaxLength(100).IsRequired();
                e.Property(x => x.Signatory2).HasMaxLength(100).IsRequired();
            });

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var propertyMethod = typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(bool));
                    var propertyCall = Expression.Call(propertyMethod, parameter, Expression.Constant("IsDeleted"));
                    var equalsExpression = Expression.Equal(propertyCall, Expression.Constant(false));
                    var lambda = Expression.Lambda(equalsExpression, parameter);
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }
        }
    }
}