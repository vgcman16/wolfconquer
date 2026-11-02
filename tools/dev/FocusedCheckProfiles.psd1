@{
    Quick = @{
        Description = 'Packet-codec subset; not a full server check.'
        HangSeconds = 90
        Filter = 'FullyQualifiedName~WolfConquer.Tests.MsgAttackPacketTests|FullyQualifiedName~WolfConquer.Tests.LegacyMsgUpdatePacketTests|FullyQualifiedName~WolfConquer.Tests.LegacySpellAnimationPacketTests'
    }
    Ticker = @{
        Description = 'Existing ticker scheduling, preparation and projection; add affected neighbors.'
        HangSeconds = 90
        Filter = 'FullyQualifiedName~LegacyRuntimeUpdateTicker|FullyQualifiedName~LegacyRuntimeTickProjectionTests'
    }
    GhostReviver = @{
        Description = 'Ghost and Reviver controls; add affected neighbors when required.'
        HangSeconds = 90
        Filter = 'FullyQualifiedName~Ghost|FullyQualifiedName~Reviver'
    }
    PackageHistory = @{
        Description = 'Package-history refusal, migration, recovery, allocation and 300-operation replay/restart controls.'
        HangSeconds = 600
        Filter = 'FullyQualifiedName~LegacyInventorySashUpgradeTests.FreshHistoryOwnerRefusesMissingChangedUnboundOrUnknownMetadataWithoutDiscardingIt|FullyQualifiedName~LegacyInventorySashUpgradeTests.RegisteredPackageCompletedHistoryExceeds256WithoutAllocatingDummyUidsAndRetainsOldReplayAcrossRestart|FullyQualifiedName~LegacyInventorySashUpgradeTests.RealCompletedV1AndPendingV2LegacyLayoutsMigrateAcrossEveryTypedCutWithoutDeletingOriginalBytes|FullyQualifiedName~LegacyInventorySashUpgradeTests.ExactKnownCompletionTailRecoversAfterArchivePublicationBeforeIndexReplacement|FullyQualifiedName~LegacyInventorySashUpgradeTests.RegisteredAllocatingPackageReclaimsOnlyGenuinelyCompletedUidPairsUnderActivePressure'
    }
}
