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
        public DbSet<ContractAnnotation> ContractAnnotations { get; set; }
        public DbSet<RentalValueGovtShareRate> RentalValueGovtShareRates { get; set; }
        public DbSet<UserNote> UserNotes { get; set; }
        public DbSet<SharingFormula> SharingFormulas { get; set; }
        public DbSet<BankList> BankLists { get; set; }
        public DbSet<PropertyType> PropertyTypes { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<AccRacBase> AccRacBases { get; set; }
        public DbSet<UserAppoint> UserAppoints { get; set; }

        public DbSet<LockDate> LockDates { get; set; }
        public DbSet<AccountingSys> AccountingSys { get; set; }
        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; }
        public DbSet<ChartOfAccountSubGroup> ChartOfAccountSubGroups { get; set; }
        public DbSet<ChartOfAccountControlAccount> ChartOfAccountControlAccounts { get; set; }
        public DbSet<IncomeStatement> IncomeStatements { get; set; }
        public DbSet<IncomeStatementSubGroup> IncomeStatementSubGroups { get; set; }
        public DbSet<ContractInvoicesEdit> ContractInvoicesEdits { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierRank> SupplierRanks { get; set; }
        public DbSet<SupplierCodePrefix> SupplierCodePrefixes { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerRank> CustomerRanks { get; set; }
        public DbSet<CollectionEntry> CollectionEntries { get; set; }
        public DbSet<Receipt> Receipts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RentalProperty>().ToTable("RentalProperties", "dbo");
            modelBuilder.Entity<PropertyGroup>().ToTable("PropertyGroups", "dbo");
            modelBuilder.Entity<ContractRiseTerm>().ToTable("ContractRiseTerms", "dbo");
            modelBuilder.Entity<ContractAnnotation>(e =>
            {
                e.ToTable("ContractAnnotations", "dbo");
                e.Property(x => x.Remarks).HasMaxLength(500).IsRequired();
                e.Property(x => x.RemarksBy).HasMaxLength(150);
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
                e.HasIndex(x => x.ContractId);
            });
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

            modelBuilder.Entity<UserAppoint>(e =>
            {
                e.ToTable("UserAppoints", "dbo");
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
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

            modelBuilder.Entity<AccountingSys>(e =>
            {
                e.ToTable("AccountingSys", "dbo");
                e.Property(x => x.ParticularName).HasMaxLength(200).IsRequired();
                e.Property(x => x.Address).HasMaxLength(500);
                e.Property(x => x.TelNo).HasMaxLength(50);
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<ChartOfAccount>(e =>
            {
                e.ToTable("ChartOfAccounts", "dbo");
                e.Property(x => x.AcctId).HasMaxLength(50);
                e.Property(x => x.AcctName).HasMaxLength(200).IsRequired();
                e.Property(x => x.GroupName).HasMaxLength(50).IsRequired();
                e.Property(x => x.SubGroup).HasMaxLength(150);
                e.Property(x => x.ControlAccount).HasMaxLength(100);
                e.Property(x => x.SectionType).HasMaxLength(100).IsRequired();
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<ChartOfAccountSubGroup>(e =>
            {
                e.ToTable("ChartOfAccountSubGroups", "dbo");
                e.Property(x => x.GroupName).HasMaxLength(50).IsRequired();
                e.Property(x => x.SubGroupName).HasMaxLength(150).IsRequired();
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<ChartOfAccountControlAccount>(e =>
            {
                e.ToTable("ChartOfAccountControlAccounts", "dbo");
                e.Property(x => x.ControlAccountName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<IncomeStatement>(e =>
            {
                e.ToTable("IncomeStatements", "dbo");
                e.Property(x => x.AcctId).HasMaxLength(50);
                e.Property(x => x.AcctName).HasMaxLength(200).IsRequired();
                e.Property(x => x.GroupName).HasMaxLength(50).IsRequired();
                e.Property(x => x.SubGroup).HasMaxLength(150);
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<IncomeStatementSubGroup>(e =>
            {
                e.ToTable("IncomeStatementSubGroups", "dbo");
                e.Property(x => x.GroupName).HasMaxLength(50).IsRequired();
                e.Property(x => x.SubGroupName).HasMaxLength(150).IsRequired();
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<ContractInvoicesEdit>(e =>
            {
                e.ToTable("ContractInvoicesEdit", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.ContractNo).HasMaxLength(50).IsRequired();
                e.Property(x => x.InvoiceNo).HasMaxLength(50).IsRequired();
                e.Property(x => x.SubInvoiceNo).HasMaxLength(50);
                e.Property(x => x.PeriodStart).HasColumnType("date");
                e.Property(x => x.PeriodEnd).HasColumnType("date");
                e.Property(x => x.DueDate).HasColumnType("date");
                e.Property(x => x.ContractStartDate).HasColumnType("date");
                e.Property(x => x.ContractEndDate).HasColumnType("date");
                e.Property(x => x.RiseDate).HasColumnType("date");
                e.Property(x => x.CreatedAt).HasColumnType("datetime");
                e.Property(x => x.ItemwithCode).HasMaxLength(200);
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.AccHead).HasMaxLength(100);
                e.HasIndex(x => new { x.ContractNo, x.InvoiceNo, x.SubInvoiceNo });
            });

            modelBuilder.Entity<Supplier>(e =>
            {
                e.ToTable("Suppliers", "dbo");
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
                e.Property(x => x.Prefix).HasMaxLength(20);
                e.Property(x => x.Rank).HasMaxLength(100);
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.Address).HasMaxLength(500);
                e.Property(x => x.Province).HasMaxLength(50);
                e.Property(x => x.City).HasMaxLength(100);
                e.Property(x => x.NtnCnic).HasMaxLength(50);
                e.Property(x => x.GSTNo).HasMaxLength(50);
                e.Property(x => x.TelNo).HasMaxLength(50);
                e.Property(x => x.MobileNo).HasMaxLength(50);
                e.Property(x => x.Representative).HasMaxLength(150);
                e.Property(x => x.IBAN).HasMaxLength(34);
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<SupplierRank>(e =>
            {
                e.ToTable("SupplierRanks", "dbo");
                e.Property(x => x.RankName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<SupplierCodePrefix>(e =>
            {
                e.ToTable("SupplierCodePrefixes", "dbo");
                e.Property(x => x.PrefixAlpha).HasMaxLength(20).IsRequired();
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<Customer>(e =>
            {
                e.ToTable("Customers", "dbo");
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
                e.Property(x => x.Prefix).HasMaxLength(20);
                e.Property(x => x.Rank).HasMaxLength(100);
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.Address).HasMaxLength(500);
                e.Property(x => x.Province).HasMaxLength(50);
                e.Property(x => x.City).HasMaxLength(100);
                e.Property(x => x.NtnCnic).HasMaxLength(50);
                e.Property(x => x.GSTNo).HasMaxLength(50);
                e.Property(x => x.TelNo).HasMaxLength(50);
                e.Property(x => x.MobileNo).HasMaxLength(50);
                e.Property(x => x.Representative).HasMaxLength(150);
                e.Property(x => x.IBAN).HasMaxLength(34);
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<CustomerRank>(e =>
            {
                e.ToTable("CustomerRanks", "dbo");
                e.Property(x => x.RankName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<CollectionEntry>(e =>
            {
                e.ToTable("CollectionEntries", "dbo");
                e.Property(x => x.ContractNo).HasMaxLength(50);
                e.Property(x => x.TenantBusiness).HasMaxLength(300);
                e.Property(x => x.InvoiceNo).HasMaxLength(100);
                e.Property(x => x.DueAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.BalanceAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.CollectionDate).HasColumnType("date");
                e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
                e.Property(x => x.TinTrn).HasMaxLength(100);
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<Receipt>(e =>
            {
                e.ToTable("Receipts", "dbo");
                e.Property(x => x.Date).HasColumnType("date");
                e.Property(x => x.Month).HasMaxLength(10);
                e.Property(x => x.Reference).HasMaxLength(100);
                e.Property(x => x.PaidFrom).HasMaxLength(150);
                e.Property(x => x.PayeeContactType).HasMaxLength(50);
                e.Property(x => x.PayeeName).HasMaxLength(300);
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.GrandTotal).HasColumnType("decimal(18,2)");
                e.Property(x => x.LinesJson).HasColumnType("nvarchar(max)");
                e.Property(x => x.AttachmentsJson).HasColumnType("nvarchar(max)");
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.ActionBy).HasMaxLength(150);
            });

            modelBuilder.Entity<Class>(e =>
            {
                e.Property(x => x.UoM).HasMaxLength(20);
            });

            modelBuilder.Entity<Base>(e =>
            {
                e.ToTable("Bases", "dbo");
                e.Property(x => x.FullName).HasColumnType("varchar(500)");
                e.Property(x => x.Code).HasColumnType("nchar(10)").IsFixedLength();
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
                if (entityType.ClrType == typeof(ContractInvoicesEdit))
                {
                    continue;
                }

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