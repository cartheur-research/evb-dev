
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace CsEvb;

public class Board : IXmlSerializable
{
  GA144[] chips_;

  public Board()
  {
    chips_ = new GA144[2];
    chips_[0] = new();
    chips_[1] = new();
  }

  public GA144 GetHostChip()
  {
    return chips_[0];
  }

  public GA144 GetTargetChip()
  {
    return chips_[1];
  }

  public GA144 GetChip(int node)
  {
    return chips_[node / 10000];
  }

  public virtual F18A.RomType GetRomType(int node)
  {
    int ind = node / 10000;
    return chips_[ind].GetRomType(node % 10000);
  }

  public void InitializeHostChipAsync(Serial ser, F18Assembler ass, string src)
  {
    byte[] buffer = new byte[3];
    F18A bootNode = GetHostChip().GetNodeFromNo(708);
    ass.Assemble(GetHostChip(), bootNode, src, false);
    int[] bootFrame = bootNode.GetBootFrame();
    ser.Rts(10);
    ser.WriteAll(buffer, bootFrame);
  }


  public XmlSchema? GetSchema()
  {
    return null;
  }

  public void ReadXmlHost(XmlReader reader)
  {
    while (reader.Read())
    {
      switch (reader.NodeType)
      {
        case XmlNodeType.EndElement:
          return;
        case XmlNodeType.Element:
          if (reader.Name == "GA144") { GetHostChip().ReadXml(reader); }
          break;
        default:
          break;
      }
    }

  }

  public void ReadXmlTarget(XmlReader reader)
  {
    while (reader.Read())
    {
      switch (reader.NodeType)
      {
        case XmlNodeType.EndElement:
          return;
        case XmlNodeType.Element:
          if (reader.Name == "GA144") { GetTargetChip().ReadXml(reader); }
          break;
        default:
          break;
      }
    }

  }

  public void ReadXmlBoard(XmlReader reader)
  {
    while (reader.Read())
    {
      switch (reader.NodeType)
      {
        case XmlNodeType.EndElement:
          return;
        case XmlNodeType.Element:
          if (reader.Name == "host") { ReadXmlHost(reader); }
          else if (reader.Name == "target") { ReadXmlTarget(reader); }
          break;
        default:
          break;
      }
    }

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
          if (reader.Name == "board") { ReadXmlBoard(reader); }
          break;
        default:
          break;
      }
    }
  }

  public void WriteXml(XmlWriter writer)
  {
    writer.WriteStartElement("board");
    writer.WriteStartElement("host");
    GetHostChip().WriteXml(writer);
    writer.WriteEndElement();
    writer.WriteStartElement("target");
    GetTargetChip().WriteXml(writer);
    writer.WriteEndElement();
    writer.WriteEndElement();
  }
}
