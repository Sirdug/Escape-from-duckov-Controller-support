namespace DuckovPad
{
    internal static class PointerFocus
    {
        // macOS can delay or reject a cursor warp. A stale absolute position alone
        // must not steal controller focus; require mouse motion as well.
        public static bool ShouldYield(bool macOS, float divergenceSquared, float deltaSquared, bool padActive = false)
            => divergenceSquared > 100f && (!macOS || (!padActive && deltaSquared > 0.01f));
    }
}
