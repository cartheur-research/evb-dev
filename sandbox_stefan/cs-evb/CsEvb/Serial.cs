
using System.Xml;

namespace CsEvb;

public class Serial
{
  private System.IO.Ports.SerialPort port_;



  static public string[] ListAll()
  {
    return System.IO.Ports.SerialPort.GetPortNames();
  }

  static public Serial Create(string name)
  {
    return new Serial(name, 115200);
    //return new Serial(name, 921600);
  }

  public Serial(string name, int baud)
  {
    port_ = new System.IO.Ports.SerialPort(name, baud);
  }

  public void Open()
  {
    port_.Open();
  }

  public void Close()
  {
    port_.Close();
  }

  public void Rts(int msDelay)
  {
    port_.RtsEnable = true;
    Thread.Sleep(msDelay);
    port_.RtsEnable = false;
  }

  public void Dtr(int msDelay)
  {
    port_.DtrEnable = true;
    Thread.Sleep(msDelay);
    port_.DtrEnable = false;
  }

  public void Break(int msDelay)
  {
    port_.BreakState = true;
    Thread.Sleep(msDelay);
    port_.BreakState = false;
  }

  public void Write(byte[] buffer, int data18) // write 18 bit value
  {
    data18 ^= 0x3ffff;
    data18 <<= 6;
    data18 |= 0x12;
    buffer[0] = (byte) (data18);
    buffer[1] = (byte) (data18 >> 8);
    buffer[2] = (byte) (data18 >> 16);
    port_.Write(buffer, 0, 3);
  }

  public void WriteAll(byte[] buffer, int[] data) // write 18 bit value
  {
    int limit = data.Length;
    for (int i = 0; i < limit; ++i)
    {
      Write(buffer, data[i]);
    }
  }

  public int Read(byte[] buffer) // write 18 bit value
  {
    port_.Read(buffer, 0, 3);
    int data18 = buffer[2];
    data18 <<= 8;
    data18 |= buffer[1];
    data18 <<= 8;
    data18 |= buffer[0];
    data18 &= 0x3ffff;
    return data18;
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
//          if (reader.Name == "board") { ReadXmlBoard(reader); }
          break;
        default:
          break;
      }
    }
  }

  public void WriteXml(XmlWriter writer)
  {
    writer.WriteStartElement("serial");
    writer.WriteStartElement("host");
//    GetHostChip().WriteXml(writer);
    writer.WriteEndElement();
    writer.WriteStartElement("target");
//    GetTargetChip().WriteXml(writer);
    writer.WriteEndElement();
    writer.WriteEndElement();
  }


}
