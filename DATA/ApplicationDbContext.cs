using ACXIOMCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ACXIOMCRM.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Lead> Leads { get; set; }
        public DbSet<FollowUp> FollowUps { get; set; }
        public DbSet<Opportunity> Opportunities { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<CrmActivity> Activities { get; set; }
        public DbSet<ManualAudit> ManualAudits { get; set; }
        public DbSet<ManualAuditEvent> ManualAuditEvents { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<IdentityUserLogin<string>>()
                .Property(login => login.LoginProvider)
                .HasMaxLength(128);
            builder.Entity<IdentityUserLogin<string>>()
                .Property(login => login.ProviderKey)
                .HasMaxLength(128);
            builder.Entity<IdentityUserToken<string>>()
                .Property(token => token.LoginProvider)
                .HasMaxLength(128);
            builder.Entity<IdentityUserToken<string>>()
                .Property(token => token.Name)
                .HasMaxLength(128);

            builder.Entity<FollowUp>()
                .HasOne(f => f.Lead)
                .WithMany(l => l.FollowUps)
                .HasForeignKey(f => f.LeadId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Opportunity>()
                .HasOne(o => o.Lead)
                .WithMany(l => l.Opportunities)
                .HasForeignKey(o => o.LeadId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Opportunity>()
                .Property(o => o.Amount)
                .HasPrecision(18, 2);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Team)
                .WithMany(t => t.Members)
                .HasForeignKey(u => u.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ApplicationUser>()
                .Property(u => u.IsActive)
                .HasDefaultValue(true);

            builder.Entity<Team>()
                .HasIndex(t => t.Name)
                .IsUnique();

            builder.Entity<Customer>()
                .HasOne(c => c.AssignedToUser)
                .WithMany()
                .HasForeignKey(c => c.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Lead>()
                .HasOne(l => l.AssignedToUser)
                .WithMany()
                .HasForeignKey(l => l.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Customer>()
                .HasOne(c => c.ConvertedFromLead)
                .WithOne(l => l.ConvertedCustomer)
                .HasForeignKey<Customer>(c => c.ConvertedFromLeadId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<CrmActivity>()
                .HasOne(a => a.Customer).WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<CrmActivity>()
                .HasOne(a => a.Lead).WithMany().HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<CrmActivity>()
                .HasOne(a => a.Opportunity).WithMany().HasForeignKey(a => a.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<CrmActivity>()
                .HasOne(a => a.AssignedToUser).WithMany().HasForeignKey(a => a.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<CrmActivity>().HasIndex(a => new { a.Status, a.ScheduledAt });

            builder.Entity<ManualAudit>()
                .HasOne(audit => audit.Auditor)
                .WithMany()
                .HasForeignKey(audit => audit.AuditorUserId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Entity<ManualAudit>().HasIndex(audit => new { audit.Status, audit.AuditDate });
            builder.Entity<ManualAudit>().HasIndex(audit => audit.AuditorUserId);
            builder.Entity<ManualAuditEvent>().HasIndex(item => item.Timestamp);
            builder.Entity<ManualAuditEvent>().HasIndex(item => item.Action);
            builder.Entity<ManualAuditEvent>().HasIndex(item => item.AuditRecordId);
        }
    }
}
