using System;
using System.Collections.Generic;
using GSLInvoicing.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GSLInvoicing.Web.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Client> Clients { get; set; }

    public virtual DbSet<Config> Configs { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Client__3214EC0727AB62F1");

            entity.ToTable("Client");

            entity.Property(e => e.City)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Contact)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Country)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(CONVERT([date],getdate()))");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.GSTCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Postcode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Rate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Street)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Suburb)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Config>(entity =>
        {
            entity.ToTable("Config");

            entity.Property(e => e.LastInvoiceNumber)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasDefaultValue("GSL0000");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoice");

            entity.Property(e => e.Contact)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(CONVERT([date],getdate()))");
            entity.Property(e => e.InvoiceDate).HasDefaultValueSql("(CONVERT([date],getdate()))");
            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Notes).IsUnicode(false);
            entity.Property(e => e.PONumber)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.Client).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ClientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoice_Client");
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.ToTable("InvoiceItem");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).IsUnicode(false);
            entity.Property(e => e.GST).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Rate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Total).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceItems)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("FK_InvoiceItem_Invoice");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
