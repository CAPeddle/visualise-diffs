using TreeSitter;
using var l = new Language("cpp"); using var p = new Parser(l);
var src = "// 日本語\nclass X {};\n/*日本語*/ class Y {};\n";
using var t = p.Parse(src)!;
foreach (var c in t.RootNode.NamedChildren) if (c.Type == "class_specifier" || c.Type=="declaration" || c.Type=="struct_specifier")
  Console.WriteLine($"{c.Type} chars: Start={c.StartIndex} End={c.EndIndex} text='{c.Text}' row={c.StartPosition.Row} col={c.StartPosition.Column} | src.Substring(Start,len)='{src.Substring(c.StartIndex, c.EndIndex-c.StartIndex)}'");
Console.WriteLine("utf8 bytes of first 'class' prefix = " + System.Text.Encoding.UTF8.GetByteCount(src.AsSpan(0, src.IndexOf("class X"))) + ", char idx = " + src.IndexOf("class X"));
Console.WriteLine("class Y char col = " + (src.Split('\n')[2].IndexOf("class Y")) + " utf8 col = " + System.Text.Encoding.UTF8.GetByteCount(src.Split('\n')[2].AsSpan(0, src.Split('\n')[2].IndexOf("class Y"))));
