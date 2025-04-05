using System.Text;
using System.Xml;

namespace CsEvb.Tests;

public class Utf8StringWriter : StringWriter
{
  public override Encoding Encoding => Encoding.UTF8;
}
public class UnitTestBoard
{

  [Fact]
  public void Test1()
  {
    string source = "Hello World";
    string comment = "<<; ]]>";

    CsEvb.Board board = new();

    CsEvb.GA144 host = board.GetHostChip();
    CsEvb.F18A node = host.GetNode(host.GetNodeIndex(1, 2));
    node.Source = source;
    node.Comment = comment;

    Utf8StringWriter sw = new();
    XmlWriterSettings wrs = new XmlWriterSettings
    {
      Indent = true,
      Encoding = System.Text.Encoding.UTF8
    };
    XmlWriter wr = XmlWriter.Create(sw, wrs);

    wr.WriteStartDocument();
    board.WriteXml(wr);
    wr.WriteEndDocument();
    wr.Flush();
    wr.Close();
    wr.Dispose();

    string xml = sw.ToString();

    StringReader srd = new StringReader(xml);

    board = new();
    XmlReader rd = XmlReader.Create(srd);
    board.ReadXml(rd);

    host = board.GetHostChip();
    node = host.GetNode(host.GetNodeIndex(1, 2));

    Assert.Equal(source, node.Source);
    Assert.Equal(comment, node.Comment);


  }

  [Fact]
  public void Test2()
  {
    CsEvb.Board board = new();
    CsEvb.GA144 host = board.GetHostChip();
    CsEvb.F18A node = host.GetNodeFromNo(708);
    CsEvb.F18Assembler ass = new();

    node = host.GetNode(host.GetNodeIndexFromNo(708));
    string txt = node.DecompileROM();

    //    node.CompileRAM(ass, """

    //""");


    string[] comList = CsEvb.Serial.ListAll();
    if (comList.Length != 1) { return; }

    CsEvb.Serial serial = CsEvb.Serial.Create(comList[0]);

    serial.Open();
    //serial.Rts(10); // reset host chip
    board.InitializeHostChipAsync(serial, ass, """

0 NL 'w
0 NL 'wr
0 NL 'w1
0 NL 'w2
0 NL 'wr1

# 0 org
: obit ( dwn-dw) !b over >r delay ;
// : feedback ( d-d) x600D
: oword ( dw-d)  leap drop  leap drop leap drop  drop ;
: obyt ( dw-dwx)  then then then  3. obit drop
	7. for dup 1. and 3. xor obit  drop 2/ next 
	2. obit ;
: kraken-w1 'w1 ! 
: ksnd2 ( -dd) leap : ksnd ( -d) then 18ibits drop ! ;
: krcv ( d-d) @ oword ;
: seta 18ibits drop a! ;
: k-w ksnd 18ibits drop dup !
: nsnd ( n) for ksnd next ;
: kraken-wr1 'wr1 ! ksnd2 krcv ;
: kraken-w2 'w2 ! ksnd2 ksnd ;
: main seta 18ibits drop nsnd  
: kraken 18ibits drop >r ex kraken ;
: kraken-w 'w ! k-w kraken ;
: kraken-wr 'wr ! k-w 18ibits drop dup !
	( d k-1) for krcv next kraken ;

# main /P

""");
  }


}