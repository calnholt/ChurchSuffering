namespace ChurchSuffering.Diagnostics.Snapshots
{
    public interface IDisplaySnapshotFixture
    {
        string Id { get; }
        int WarmupFrames { get; }
        void Setup(DisplaySnapshotContext ctx, string[] args);
        void Draw(DisplaySnapshotContext ctx);
        string OutputFileName { get; }
    }
}
