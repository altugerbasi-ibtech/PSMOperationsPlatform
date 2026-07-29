using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class ManagedServerConfiguration : IEntityTypeConfiguration<ManagedServer>
{
    public void Configure(EntityTypeBuilder<ManagedServer> builder)
    {
        builder.ToTable(
            "ManagedServer",
            "configuration",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ManagedServer_WinRmTransportMode",
                    "[WinRmTransportMode] IN ('Auto', 'HttpsOnly', 'HttpOnly')");
                table.HasCheckConstraint(
                    "CK_ManagedServer_WinRmHttpsPort_Range",
                    "[WinRmHttpsPort] >= 1 AND [WinRmHttpsPort] <= 65535");
                table.HasCheckConstraint(
                    "CK_ManagedServer_WinRmHttpPort_Range",
                    "[WinRmHttpPort] >= 1 AND [WinRmHttpPort] <= 65535");
                table.HasCheckConstraint(
                    "CK_ManagedServer_WinRmProbeTimeout_Positive",
                    "[WinRmProbeTimeoutSeconds] > 0");
                table.HasCheckConstraint(
                    "CK_ManagedServer_LastConnectivityState",
                    "[LastConnectivityState] IN ('Unknown', 'Reachable', 'Unreachable')");
                table.HasCheckConstraint(
                    "CK_ManagedServer_ConnectivityFailures_NonNegative",
                    "[ConsecutiveConnectivityFailures] >= 0");
                table.HasCheckConstraint(
                    "CK_ManagedServer_InventoryFailures_NonNegative",
                    "[ConsecutiveInventoryFailures] >= 0");
                table.HasCheckConstraint(
                    "CK_ManagedServer_InventoryVersion_NonNegative",
                    "[InventoryVersion] >= 0");
                table.HasCheckConstraint(
                    "CK_ManagedServer_LastInventoryFailureCategory",
                    "[LastInventoryFailureCategory] IS NULL OR [LastInventoryFailureCategory] IN ('CollectionFailure', 'ParsingFailure', 'ValidationFailure', 'PersistenceFailure', 'Timeout', 'Unexpected')");
                table.HasCheckConstraint(
                    "CK_ManagedServer_LastSuccessfulTransport",
                    "[LastSuccessfulTransport] IS NULL OR [LastSuccessfulTransport] IN ('Https', 'Http')");
                table.HasCheckConstraint(
                    "CK_ManagedServer_LastConnectivityFailureCategory",
                    "[LastConnectivityFailureCategory] IS NULL OR [LastConnectivityFailureCategory] IN ('DnsFailure', 'ConnectionRefused', 'Timeout', 'TlsFailure', 'AuthenticationFailure', 'AuthorizationFailure', 'WinRmUnavailable', 'ProtocolFailure', 'Unexpected')");
            });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.Fqdn).HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(255);
        builder.Property(entity => entity.Environment).HasMaxLength(50);
        builder.Property(entity => entity.IsEnabled).IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(entity => entity.NextConnectivityAttemptAt)
            .HasColumnType("datetime2(3)");
        builder.Property(entity => entity.WinRmTransportMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(entity => entity.WinRmHttpsPort).IsRequired();
        builder.Property(entity => entity.WinRmHttpPort).IsRequired();
        builder.Property(entity => entity.WinRmProbeTimeoutSeconds).IsRequired();
        builder.Property(entity => entity.LastConnectivityState)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(entity => entity.LastConnectivityAttemptAt)
            .HasColumnType("datetime2(3)");
        builder.Property(entity => entity.LastConnectivitySuccessAt)
            .HasColumnType("datetime2(3)");
        builder.Property(entity => entity.LastSuccessfulTransport)
            .HasConversion<string>()
            .HasMaxLength(10);
        builder.Property(entity => entity.ConsecutiveConnectivityFailures)
            .IsRequired();
        builder.Property(entity => entity.LastConnectivityFailureCategory)
            .HasConversion<string>()
            .HasMaxLength(40);
        builder.Property(entity => entity.LastInventoryAttemptAt)
            .HasColumnType("datetime2(7)");
        builder.Property(entity => entity.LastInventorySuccessAt)
            .HasColumnType("datetime2(7)");
        builder.Property(entity => entity.NextInventoryAttemptAt)
            .HasColumnType("datetime2(7)");
        builder.Property(entity => entity.ConsecutiveInventoryFailures)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(entity => entity.LastInventoryFailureCategory)
            .HasMaxLength(80);
        builder.Property(entity => entity.InventoryVersion)
            .HasDefaultValue(0L)
            .IsRequired();
        builder.Property(entity => entity.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(entity => entity.Fqdn)
            .IsUnique()
            .HasDatabaseName("UX_ManagedServer_Fqdn");
        builder.HasIndex(entity => new
        {
            entity.IsEnabled,
            entity.NextConnectivityAttemptAt,
        })
            .HasDatabaseName("IX_ManagedServer_Eligibility");
        builder.HasIndex(entity => new
        {
            entity.IsEnabled,
            entity.NextInventoryAttemptAt,
        })
            .HasDatabaseName("IX_ManagedServer_InventoryEligibility");
    }
}
