using System;

namespace ISC.iNet.DS.Services
{
    internal class PrinterCE
    {
        public bool FontBold { get; internal set; }
        public static object EXCEPTION_LEVEL { get; internal set; }
        public object PrDialogBox { get; internal set; }
        public object SetReportLevel { get; internal set; }
        public static object REPORT_LEVEL { get; internal set; }
        public string FontName { get; internal set; }
        public int FontSize { get; internal set; }
        public object GetLastError { get; internal set; }
        public object JustifyHoriz { get; internal set; }

        internal void DrawText(string label, int labelX, int y)
        {
            throw new NotImplementedException();
        }

        internal void DrawRect(int v1, int topY, int v2, int bottomY)
        {
            throw new NotImplementedException();
        }

        internal void EndDoc()
        {
            throw new NotImplementedException();
        }

        internal void SetupPrinter(object hP_PCL, object lPT, bool v)
        {
            throw new NotImplementedException();
        }

        internal void DrawLine(int v1, int v2, int v3, int v4)
        {
            throw new NotImplementedException();
        }

        internal int GetStringWidth(string v)
        {
            throw new NotImplementedException();
        }
    }
}