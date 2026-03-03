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