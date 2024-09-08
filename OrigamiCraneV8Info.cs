using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace OrigamiCraneV8
{
    public class OrigamiCraneV8Info : GH_AssemblyInfo
    {
        public override string Name => "OrigamiCraneV8";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("60f131c7-a2e6-4366-91c8-fa23f4fb0fff");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}