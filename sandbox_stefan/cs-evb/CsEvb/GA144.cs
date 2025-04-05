using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace CsEvb;

public class GA144 : Chip, IXmlSerializable
{
  public const int NoOfRows = 8;
  public const int NoOfColumns = 18;

  private readonly F18A[] nodes_;
  private readonly F18Assembler assembler_ = new();

  public int GetNodeIndex(int row, int column)
  {
    return column * NoOfRows + row;
  }

  public int GetNodeNo(int row, int column)
  {
    return row * 100 + column;
  }

  public void GetNodePlaceFromIndex(int node_index, out int row, out int column)
  {
    row = node_index % NoOfRows;
    column = node_index / NoOfRows;
  }

  public void GetNodePlaceFromNo(int node_no, out int row, out int column)
  {
    row = (node_no / 100) % 100;
    column = node_no % 100;
  }

  public int GetNodeIndexFromNo(int node_no)
  {
    GetNodePlaceFromNo(node_no, out int r, out int c);
    return GetNodeIndex(r, c);
  }

  public int GetNodeNoFromIndex(int node_index)
  {
    GetNodePlaceFromIndex(node_index, out int r, out int c);
    return GetNodeNo(r, c);
  }

  public F18A GetNodeFromNo(int node_no)
  {
    return nodes_[GetNodeIndexFromNo(node_no)];
  }

  public F18A GetNode(int node_index)
  {
    return nodes_[node_index];
  }

  public void SetNode(int node_index, F18A node)
  {
    nodes_[node_index] = node;
  }

  public override int GetNoOfRows()
  {
    return NoOfRows;
  }

  public override int GetNoOfColumns()
  {
    return NoOfColumns;
  }

  public override F18A.RomType GetRomType(int node_no)
  {
    switch (node_no)
    {
      case 007: return F18A.RomType.Node007;
      case 008: return F18A.RomType.Node008;
      case 009: return F18A.RomType.Node009;
      case 106: return F18A.RomType.Node106;
      case 107: return F18A.RomType.Node107;
      case 108: return F18A.RomType.Node108;
      case 001:
      case 701: return F18A.RomType.SerdesBoot;
      case 705: return F18A.RomType.SpiBoot;
      case 300: return F18A.RomType.SyncBoot;
      case 708: return F18A.RomType.AsyncBoot;
      case 117:
      case 617:
      case 717:
      case 713:
      case 709: return F18A.RomType.Analog;
      case 100:
      case 500:
      case 600:
      case 417:
      case 317: return F18A.RomType.Digital;
      case 200: return F18A.RomType.Wire1;
      default: break;
    }
    return F18A.RomType.Basic;
  }

  /// <summary>
  /// Constructor defining the ROM code (incl. labels) of every node.
  /// </summary>
  public GA144()
  {
    int size = NoOfRows * NoOfColumns;
    nodes_ = new F18A[size];

    //string rom;
    F18A node;
    //F18Assembler ass = new();

    for (int i = 0; i < size; i++)
    {
      node = new F18A();
      node.SetChip(this);
      node.SetIndex(i);
      //node.ClearROM();
      //rom = node.GetROMSource();
      //assembler_.Assemble(this, node, rom, true);
    }

    // these nodes are used by other nodes
    nodes_[GetNodeIndexFromNo(008)].CompileROM(assembler_);
    nodes_[GetNodeIndexFromNo(007)].CompileROM(assembler_);

    for (int i = 0; i < size; i++)
    {
      node = nodes_[i];
      node.CompileROM(assembler_);
    }

      //for (int r = 0; r < NoOfRows; ++r)
      //{
      //  for (int c = 0; c < NoOfColumns; ++c)
      //  {
      //    node = new F18A();
      //    node.Set(this, r, c);
      //    nodes_[GetIndex(r, c)] = node;
      //    node.ClearROM();
      //    node.SetRomType(GetRomType(node.GetNodeNo()));
      //    rom = node.GetROMSource();
      //    assembler_.Assemble(this, node, rom, true);
      //  }
      //}



      //if (false)
      //{
    //  node = nodes_[GetIndex(1, 7)];
    //Console.WriteLine("-------------- ROM -------------------");
    //Console.WriteLine(assembler_.Disassemble(node, 0x80, 0xBF));
    //rom = node.GetROMSource();
    //assembler_.Assemble(this, node, rom, true);
    //Console.WriteLine("-------------- ROM -------------------");
    //}
  }

  //public void Compile(int row, int column)
  //{
  //  F18A node;
  //  node = nodes_[GetIndex(row, column)];
  //  node.ClearRAM();
  //  assembler_.Assemble(this, node, node.Source, false);
  //}


  public XmlSchema? GetSchema()
  {
    return null;
  }

  public void ReadXml(XmlReader reader)
  {
    while (reader.Read())
    {
      switch (reader.NodeType)
      {
        case XmlNodeType.EndElement:
          return;
        case XmlNodeType.Element:
          if (reader.Name == "F18A")
          {
            F18A node = new();
            node.SetChip(this);
            node.ReadXml(reader);
          }
          break;
        default:
          break;
      }
    }
  }

  public void WriteXml(XmlWriter writer)
  {
    writer.WriteStartElement("GA144");
    foreach(var node in nodes_)
    {
      node.WriteXml(writer);
    }
    writer.WriteEndElement();
  }

  public void FillHostTest()
  {
    F18A node = GetNodeFromNo(708);
    node.Source = @"
    ";
  }


}
