namespace NavalCombatCore
{
    public class MaskCheckResult
    {
        public bool isMasked;
        public object maskedObject;
        public string message;
    }

    // IMaskProvider should look at NavalGameState's data, such as location of ships and size, to determine if LOS is masked.
    // The object which is at src location would not block LOS.
    public interface IMaskProvider
    {
        public MaskCheckResult Check(LatLon src, LatLon dst);
    }

    public class FallbackMaskProvider : IMaskProvider
    {
        public MaskCheckResult Check(LatLon src, LatLon dst)
        {
            return new() { isMasked = false };
        }
    }

    public class MaskService
    {
        public IMaskProvider maskProvider = new FallbackMaskProvider();
        static MaskService instance = new();
        public static MaskService Instance
        {
            get => instance;
        }

        public MaskCheckResult Check(LatLon src, LatLon dst)
        {
            return maskProvider.Check(src, dst);
        }
    }
}