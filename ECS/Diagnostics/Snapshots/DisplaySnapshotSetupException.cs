using System;

namespace ChurchSuffering.Diagnostics.Snapshots
{
    public sealed class DisplaySnapshotSetupException : Exception
    {
        public DisplaySnapshotSetupException(string message) : base(message) { }
    }
}
