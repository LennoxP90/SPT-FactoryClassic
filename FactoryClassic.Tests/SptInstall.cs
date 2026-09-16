using System;
using System.IO;

namespace FactoryClassic.Tests;

// Where the tests that compare against the SHIPPED tables read them from. Every caller guards with
// File.Exists, so a machine without an SPT install skips those and runs the rest of the suite.
static class SptInstall
{
    public static string Root =>
        Environment.GetEnvironmentVariable("FACTORYCLASSIC_SPT_ROOT") ?? @"C:\SPT C\SPT-4.1-Clean";

    public static string Database => Path.Combine(Root, "SPT_Runtime", "SPT_Data", "database");

    public static string Locations(string map, string file) => Path.Combine(Database, "locations", map, file);

    public static string Template(string file) => Path.Combine(Database, "templates", file);
}
