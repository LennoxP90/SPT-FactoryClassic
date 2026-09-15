namespace FactoryClassic.Client
{
    // What the config dropdown binds to. Deliberately NOT called MapVariant: a client type of that
    // name shadows Shared.MapVariant inside this namespace, and every file that then wants the wire
    // constants has to qualify them. That cost three separate compile errors before the rename.
    //
    // The member names are what a player reads in ConfigurationManager, so they use the display
    // vocabulary; Shared.MapVariant holds the values that go over the wire.
    internal enum FactoryChoice
    {
        Vanilla,
        Classic,
    }
}
