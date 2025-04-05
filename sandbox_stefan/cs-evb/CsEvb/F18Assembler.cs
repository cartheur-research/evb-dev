using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection.Emit;
using System.Text;

namespace CsEvb
{
  public class F18Assembler
  {
    private Stack<int> stack_ = [];
    private Stack<int> rstack_ = [];
    private F18A? node_ = null; // compilation target
    private GA144? chip_ = null; // target chip
    private string current_ = ""; // current text
    private System.Text.StringBuilder bld_ = new();
    private Dictionary<string, int> constants_ = [];
    private int src_pos_ = 0; // position in source
    private int start_pos_ = 0; // start position of current word in source
    private int cell_ = 0; // current cell
    private int slot_ = 0; // current slot
    private int pos_ = -1; // current position of cell
    private int next_pos_ = -1; // next position of cell
    private int last_call_pos_ = 0;
    private int last_call_cell_ = 0;
    private int last_call_slot_ = 0;
    private bool is_skipping_ = false; // used for conditional compilation
    private bool is_skipping_else_ = false; // used for conditional compilation
    private bool is_compiling_ = false; // flag indication that compiling to current node is enabled
    private bool is_inlining_ = false; // flag indication that assembler is in inlining mode entered with A[ ( leave with ]] )
    private bool is_finished_ = false; // flag indication that source text is finished
    private bool last_was_call_ = false; // flag indication that last instruction was a call
    private bool new_last_was_call_ = false; // flag indication that current instruction was a call

    public enum Command
    {
      undefined,
      //ADD,              // +
      SUB,              // -
      MUL,              // *
      DIV,              // /
      MOD,              // mod
      DIVMOD,           // /mod
      INVERT,           // invert
      NEGATE,           // negate
      ABS,              // abs
      NUMBER,           // #
      COLON,            // :
      DOT,              // .
      SKIP,             // ..
      LESSSL,           // <sl
      COMMA,            // ,
      SEMICOLON,        // ;
      COMMENT,          // (
      PCY,              // +cy
      MCY,              // -cy
      ABEGIN,           // A[
      SETP,             // =P
      ENDA,             // ]]
      LCOMMENT,         // line comment //
      HERE,             // here
      BEGIN,            // begin
      ELSE,             // else
      THEN,             // then
      IF,               // if
      WHILE,            // while
      UNTIL,            // until
      MIF,              // -if
      MWHILE,           // -while
      MUNTIL,           // -until
      ZIF,              // zif
      AHEAD,            // ahead
      LEAP,             // leap
      AGAIN,            // again
      REPEAT,           // repeat
      FOR,              // for
      NEXT,             // next
      UNEXT,            // unext
      SNEXT,            // *next
      ORG,              // org
      QORG,             // ?org ( n-) error if org is not equal to expected value
      NL,               // NL
      NDL,              // NDL
      EQU,              // equ (label definition)
      ITS,              // its
      SWAP,             // SWAP
      LIT,              // lit
      ALIT,             // alit
      AWAIT,            // await
      AVAIL,            // avail
      LOAD,             // LOAD
      ASSIGN_A,         // /A ( n ) define register A
      ASSIGN_B,         // /B ( n ) define register B
      ASSIGN_IO,        // /IO ( n ) define register IO
      ASSIGN_P,         // /P ( n ) define register P
      ASSIGN_STACK,     // /STACK ( n0 .. n9 ) define parameter stack content
      ASSIGN_RSTACK,    // /RSTACK ( n0 .. n8 ) define return stack content
      DO_ADD,           // [+]
      //DO_SUB,           // [-]
      //DO_MUL,           // [*]
      //DO_DIV,           // [/]
      AND,           // AND
      OR,            // OR
      XOR,           // XOR
      INV,           // INV
      DO_IF,            // #IF
      DO_ELSE,          // #ELSE
      DO_THEN,          // #THEN
      TO_INTERPRET,     // [
      TO_COMPILE,       // ]
      INCLUDE,  // include  ( node ) import labels defined by node (and compile if not already compiled)
      ASSERT,  // assert  ( n ) assert that slot is 0 and the current address is n
      TICK, // ' <label> ( - n ) lookup label and push its value onto the stack
      limit
    }

    static private readonly Dictionary<string, Command> command_map_g;
    static private readonly Dictionary<string, F18A.Opcode> opcode_map_g;
    private static readonly Dictionary<string, int> constant_map_g;

    static F18Assembler()
    {
      command_map_g = [];
      opcode_map_g = [];
      constant_map_g = [];


      foreach (Command cmd in Enum.GetValues(typeof(Command)))
      {
        string? txt = CommandToString(cmd);
        if (txt is null)
        {
          //throw new InvalidProgramException("missing text for Command '" + cmd.ToString() + "'");
          Console.WriteLine("missing text for Command '" + cmd.ToString() + "'");
        }
        else
        {
          command_map_g[txt] = cmd;
        }
      }

      foreach (F18A.Opcode op in Enum.GetValues(typeof(F18A.Opcode)))
      {
        string? txt = F18A.OpcodeToString(op);
        if (txt is null)
        {
          //throw new InvalidProgramException("missing text for Command '" + cmd.ToString() + "'");
          Console.WriteLine("missing text for Opcode '" + op.ToString() + "'");
        }
        else
        {
          opcode_map_g[txt] = op;
        }
      }

      for (int i=0x100; i<= 0x1ff; ++i)
      {
        string? name = F18A.AddrToString(i);
        if (name is not null)
        {
          constant_map_g[name] = i;
        }
        name = F18A.IOToString(i);
        if (name is not null)
        {
          constant_map_g[name] = i;
        }
      }

      // alias
      opcode_map_g[">r"] = F18A.Opcode.PUSH;
      opcode_map_g["r>"] = F18A.Opcode.POP;
    }

    public static Command? CommandFromString(string txt)
    {
      if (command_map_g.TryGetValue(txt, out var res)) { return res; }
      return null;
    }

    public static F18A.Opcode? OpcodeFromString(string txt)
    {
      if (opcode_map_g.TryGetValue(txt, out var res)) { return res; }
      return null;
    }

    public static string? CommandToString(Command cmd)
    {
      switch (cmd)
      {
        //case Command.ADD: return "+";
        case Command.SUB: return "-";
        case Command.MUL: return "*";
        case Command.DIV: return "/";
        case Command.MOD: return "MOD";
        case Command.DIVMOD: return "/MOD";
        case Command.NUMBER: return "#";
        case Command.COLON: return ":";
        case Command.DOT: return ".";
        case Command.SKIP: return "..";
        case Command.LESSSL: return "<sl";
        case Command.COMMA: return ",";
        case Command.SEMICOLON: return ";";
        case Command.COMMENT: return "(";
        case Command.PCY: return "+cy";
        case Command.MCY: return "-cy";
        case Command.ABEGIN: return "A[";
        case Command.SETP: return "=P";
        case Command.ENDA: return "]]";
        case Command.LCOMMENT: return "//";
        case Command.HERE: return "here";
        case Command.BEGIN: return "begin";
        case Command.ELSE: return "else";
        case Command.THEN: return "then";
        case Command.IF: return "if";
        case Command.WHILE: return "while";
        case Command.UNTIL: return "until";
        case Command.MIF: return "-if";
        case Command.MWHILE: return "-while";
        case Command.MUNTIL: return "-until";
        case Command.ZIF: return "zif";
        case Command.AHEAD: return "ahead";
        case Command.AVAIL: return "avail";
        case Command.LEAP: return "leap";
        case Command.AGAIN: return "again";
        case Command.REPEAT: return "repeat";
        case Command.FOR: return "for";
        case Command.NEXT: return "next";
        case Command.UNEXT: return "unext";
        case Command.SNEXT: return "*next";
        case Command.ORG: return "org";
        case Command.QORG: return "?org";
        case Command.NL: return "NL";
        case Command.NDL: return "NDL";
        case Command.EQU: return "equ";
        case Command.ITS: return "its";
        case Command.SWAP: return "SWAP";
        case Command.LIT: return "lit";
        case Command.ALIT: return "alit";
        case Command.AWAIT: return "await";
        case Command.LOAD: return "LOAD";
        case Command.ASSIGN_A: return "/A";
        case Command.ASSIGN_B: return "/B";
        case Command.ASSIGN_IO: return "/IO";
        case Command.ASSIGN_P: return "/P";
        case Command.INCLUDE: return "include";
        case Command.DO_ADD: return "[+]";
        //case Command.DO_SUB: return "[-]";
        //case Command.DO_MUL: return "[*]";
        //case Command.DO_DIV: return "[/]";
        case Command.AND: return "AND";
        case Command.OR: return "OR";
        case Command.XOR: return "XOR";
        case Command.INV: return "INV";
        case Command.DO_IF: return "#IF";
        case Command.DO_ELSE: return "#ELSE";
        case Command.DO_THEN: return "#THEN";
        default: break;
      }
      return null;
    }

    public int Position { get { return src_pos_; } }
    public int StartPosition { get { return start_pos_; } }

    public string Disassemble(F18A node, int pos, int end_pos)
    {
      node_ = node;
      bld_.Clear();
      while (pos < end_pos)
      {
        pos = Decompile(pos, node_.Read(pos));
      }
      Decompile(pos, node_.Read(pos));
      return bld_.ToString();
    }

    public int GetSlot(int cell, int slot)
    {
      switch (slot)
      {
        case 0: return (cell >> 13) & 0x1f;
        case 1: return (cell >> 8) & 0x1f;
        case 2: return (cell >> 3) & 0x1f;
        default: break;
      }
      return (cell & 0x7) << 2;
    }

    public int GetAddress(int addr, int cell, int slot)
    {
      int mask;
      switch (slot)
      {
        case 0: mask = 0x3ff; break;
        case 1: mask = 0xff; break;
        //case 2: mask = 0x7; break;
        default: mask = 0x7; break;
      }
      return (cell & mask) | (addr & ~mask);
    }

    public int Decompile(int addr, int cell)
    {
      System.Diagnostics.Debug.Assert(node_ is not null);
      string label = "";
      int next_addr = IncAddr(addr);
      int slot = 0;
      int dest;
      bld_.Append(addr.ToString("X2"));
      bld_.Append(' ');
      if (!node_.TryLookupAddress(addr, out label)) { label = ""; }
      bld_.Append(label.PadRight(20));
      bld_.Append(' ');
      bld_.Append(cell.ToString("X5"));
      bld_.Append(' ');
      while (slot < 4)
      {
        bld_.Append(' ');
        F18A.Opcode op = (F18A.Opcode)GetSlot(cell ^ 0x15555, slot);
        switch (op)
        {
          case F18A.Opcode.CALL:
          case F18A.Opcode.JUMP:
          case F18A.Opcode.IF:
          case F18A.Opcode.MIF:
          case F18A.Opcode.NEXT:
            bld_.Append(F18A.OpcodeToString(op));
            bld_.Append(' ');
            dest = GetAddress(next_addr, cell, slot);
            bld_.Append(dest.ToString("X"));

            if (node_.TryLookupAddress(dest, out label))
            {
              bld_.Append(' ');
              bld_.Append(label);
            }
            slot = 3;
            break;

          case F18A.Opcode.RET:
            bld_.Append(F18A.OpcodeToString(op));
            slot = 3;
            break;

          case F18A.Opcode.NOP:
            bld_.Append('.');
            break;

          //case F18A.Opcode.FETCHP:
          //  int value = node_.Read(next_addr);
          //  bld_.Append(" (# ");
          //  bld_.Append(next_addr.ToString("X2"));
          //  bld_.Append(": x");
          //  //bld_.Append(value.ToString("X"));
          //  //bld_.Append(" x");
          //  //value ^= 0x15555;
          //  bld_.Append(value.ToString("X"));
          //  bld_.Append(" )");
          //  //next_addr = IncAddr(next_addr);
          //  break;

          default:
            bld_.Append(F18A.OpcodeToString(op));
            break;
        }
        ++slot;
      }
      bld_.AppendLine();
      return next_addr;
    }

    public void Assemble(GA144 chip, F18A node, string source, bool is_rom)
    {
      chip_ = chip;
      node_ = node;
      Compile(source, is_rom);
      chip_ = null;
      node_ = null;
    }

    public string Disassemble(GA144 chip, F18A node, bool is_rom)
    {
      chip_ = chip;
      node_ = node;
      string res = Decompile(is_rom);
      chip_ = null;
      node_ = null;
      return res;
    }

    public int ToDigit(char ch, int radix)
    {
      if (radix > 10)
      {
        if ((ch >= 'a') && (ch < ('a' + radix - 10)))
        {
          return ch - 'a' + 10;
        }
        if ((ch >= 'A') && (ch < ('A' + radix - 10)))
        {
          return ch - 'A' + 10;
        }
        if ((ch >= '0') && (ch <= '9'))
        {
          return ch - '0';
        }
      }
      else if ((ch >= '0') && (ch < ('0' + radix)))
      {
        return ch - '0';
      }
      return -1;
    }


    public bool LookupNumber(string txt, out int val)
    {
      System.Diagnostics.Debug.Assert(node_ is not null);
      if (node_.TryLookupLabel(txt, out val))
      {
        return true;
      }
      if (node_.TryLookupLiteral(txt, out val))
      {
        return true;
      }
      if (constant_map_g.TryGetValue(txt, out val))
      {
        return true;
      }
      val = 0;
      return false;
    }
    public bool ConvertToNumber(string txt, out int val)
    {
      bool is_neg = false;
      int limit = txt.Length;
      int radix = 10;
      val = 0;
      if (limit > 0)
      {
        int i = 0;
        if (i<limit)
        {
          switch (txt[i])
          {
            case '+': ++i; break;
            case '-': is_neg = true; ++i; break;
            default: break;
           }
        }
        if (i < limit)
        {
          switch (txt[i])
          {
            case '%': radix = 2; break;

            case '$':
            case 'x':
            case 'X':
              radix = 16;
              ++i;
              break;
            default: break;
          }
        }
        if (i >= limit) { return false; }
        while (i < limit)
        {
          char ch = txt[i++];
          int dval = ToDigit(ch, radix);
          if (dval >= 0)
          {
            val *= radix;
            val += dval;
          }
          else
          {
            switch (ch)
            {
              case '.':
              break;

              case 'R':
              case 'r':
                if ((val < 2) || (val > 36))
                {
                  return false;
                }
                radix = val;
                val = 0;
                break;

              default:
                return false;
            }
          }
        }
        if (is_neg) { val = -val; }
        val &= 0x3ffff;
        return true;
      }
      return false;
    }


    public bool ScanNext(string src, ref int pos, out int start_pos, char del, StringBuilder bld)
    {
      bld.Clear();
      int limit = src.Length;
      bool del_is_space = del == ' ';
      if (del_is_space)
      {
        while ((pos < limit) && (src[pos] <= ' ')) { ++pos; }
      }
      else
      {
        while ((pos < limit) && ((src[pos] <= ' ') || (src[pos] == del))) { ++pos; }
      }
      if (pos >= limit)
      {
        start_pos = pos;
        return false;
      }
      start_pos = pos;
      if (del_is_space)
      {
        while ((pos < limit) && (src[pos] > ' '))
        {
          bld.Append(src[pos++]);
        }
      }
      else
      {
        while ((pos < limit) && (src[pos] != del))
        {
          bld.Append(src[pos++]);
        }
        ++pos;
      }
      return true;
    }

    public bool SkipNext(string src, ref int pos, char del)
    {
      int limit = src.Length;
      bool del_is_space = del == ' ';
      if (del_is_space)
      {
        while ((pos < limit) && (src[pos] <= ' ')) { ++pos; }
      }
      else
      {
        while ((pos < limit) && ((src[pos] <= ' ') || (src[pos] == del))) { ++pos; }
      }
      if (pos >= limit)
      {
        return false;
      }
      if (del_is_space)
      {
        while ((pos < limit) && (src[pos] > ' '))
        {
          ++pos;
        }
      }
      else
      {
        while ((pos < limit) && (src[pos] != del))
        {
          ++pos;
        }
        ++pos;
      }
      return true;
    }

    public int Pop()
    {
      return stack_.Pop();
    }

    public void Push(int value)
    {
      stack_.Push(value);
    }

    public void CompileCommand(Command cmd, bool is_rom)
    {
      System.Diagnostics.Debug.Assert(node_ is not null);
      int par1, par2;
      switch (cmd)
      {
        //case Command.ADD:
        //  if (is_compiling_) { CompileOpcode(F18A.Opcode.ADD); }
        //  else { par2 = Pop(); par1 = Pop(); Push(par1 + par2); }
        //  break;

        case Command.DO_ADD:
          par2 = Pop();
          par1 = Pop();
          Push(par1 + par2);
          break;

        case Command.SUB:
          par2 = Pop();
          par1 = Pop();
          Push(par1 - par2);
          break;

        case Command.MUL:
          par2 = Pop();
          par1 = Pop();
          Push(par1 * par2);
          break;

        case Command.DIV:
          par2 = Pop();
          par1 = Pop();
          Push(par1 / par2);
          break;

        case Command.MOD:
          par2 = Pop();
          par1 = Pop();
          Push(par1 % par2);
          break;

        case Command.SWAP:
          par2 = Pop();
          par1 = Pop();
          Push(par2);
          Push(par1);
          break;

        case Command.NUMBER:
          if (!ScanNext(current_, ref src_pos_, out start_pos_, ' ', bld_))
          {
            throw new InvalidDataException("missing number");
          }
          string txt = bld_.ToString();
          if (LookupNumber(txt, out par1)) { }
          else if (ConvertToNumber(txt, out par1)) { }
          else
          {
            if (txt == "await")
            {
              par1 = node_.Warm();
            }
            else
            {
              throw new InvalidDataException("text '" + bld_.ToString() + "' cannot be converted to a number");
            }
          }
          Push(par1);
          break;

        case Command.ORG:
          FlushNop();
          pos_ = stack_.Pop();
          next_pos_ = IncAddr(pos_);
          break;

        case Command.COLON:
          FlushNop();
          Word(' ');
          RegisterLabel(bld_.ToString(), pos_, is_rom);
          is_compiling_ = true;
          break;

        case Command.EQU:
          Word(' ');
          RegisterLabel(bld_.ToString(), Pop(), is_rom);
          break;

        case Command.COMMENT:
          Word(')');
          break;

        case Command.LCOMMENT:
          Word((char)0x0a);
          break;

        case Command.IF:
          Push(PrepareBranch(F18A.Opcode.IF));
          break;

        case Command.ZIF:
          Push(PrepareBranch(F18A.Opcode.NEXT));
          break;

        case Command.MIF:
          Push(PrepareBranch(F18A.Opcode.MIF));
          break;

        case Command.AHEAD:
          Push(PrepareBranch(F18A.Opcode.JUMP));
          break;

        case Command.LEAP:
          Push(PrepareBranch(F18A.Opcode.CALL));
          break;

        case Command.THEN:
          FlushNop();
          Fixup(Pop(), pos_);
          break;

        case Command.ELSE:
          int fixup = Pop();
          Push(PrepareBranch(F18A.Opcode.JUMP));
          Fixup(fixup, pos_);
          break;

        case Command.FOR:
          CompileOpcode(F18A.Opcode.PUSH);
          FlushNop();
          Push(pos_);
          break;

        case Command.BEGIN:
          FlushNop();
          Push(pos_);
          break;

        case Command.UNEXT:
          par1 = Pop();
          if (par1 != pos_)
          {
            throw new InvalidDataException("unext not possible");
          }
          CompileOpcode(F18A.Opcode.UNEXT);
          break;

        case Command.NEXT:
          par1 = Pop();
          CompileBranch(F18A.Opcode.NEXT, par1);
          break;

        case Command.SEMICOLON:
          if (last_was_call_)
          {
            PatchLastCall();
          }
          else
          {
            CompileOpcode(F18A.Opcode.RET);
            Flush();
          }
          break;

        case Command.DOT:
          CompileOpcode(F18A.Opcode.NOP);
          break;

        case Command.PCY:
          FlushNop();
          pos_ |= 0x200;
          next_pos_ |= 0x200;
          break;

        case Command.MCY:
          FlushNop();
          pos_ &= ~0x200;
          next_pos_ &= ~0x200;
          break;


        case Command.AWAIT:
          par1 = node_.Warm();
          CompileBranch(F18A.Opcode.CALL, par1);
          break;

        case Command.UNTIL:
          CompileBranch(F18A.Opcode.IF, Pop());
          break;

        case Command.MUNTIL:
          CompileBranch(F18A.Opcode.MIF, Pop());
          break;

        case Command.SNEXT:
          par2 = Pop();
          par1 = Pop();
          Push(par2);
          CompileBranch(F18A.Opcode.NEXT, par1);
          break;

        case Command.LIT:
          CompileLiteral(Pop());
          break;

        case Command.NL:
          Word(' ');
          RegisterLiteral(bld_.ToString(), Pop(), is_rom);
          break;

        case Command.SKIP:
          FlushNop();
          break;

        case Command.ITS:
          System.Diagnostics.Debug.Assert(chip_ is not null);
          Word(' ');
          par2 = Pop();
          F18A target = chip_.GetNode(chip_.GetNodeIndexFromNo(par2));
          if (!target.TryLookup(bld_.ToString(), out par1))
          {
            throw new InvalidDataException("cannot find identifier '" + bld_.ToString() + "' in node "+ par2.ToString());
          }
          CompileBranch(F18A.Opcode.CALL, par1);
          break;

        case Command.ASSIGN_P:
          node_.SetInitialP(Pop());
          break;

        case Command.ABEGIN:
          if (is_inlining_)
          {
            throw new InvalidDataException("A[ cannot be nested");
          }
          Push(cell_);
          Push(slot_);
          Push(pos_);
          Push(next_pos_);
          cell_ = 0x15555;
          slot_ = 0;
          is_inlining_ = true;
          break;

        case Command.SETP:
          if (!is_inlining_)
          {
            throw new InvalidDataException("missing A[ before =P");
          }
          pos_ = Pop();
          next_pos_ = IncAddr(pos_);
          break;

        case Command.ENDA:
          if (!is_inlining_)
          {
            throw new InvalidDataException("missing A[ before ]]");
          }
          int inlined = 0x15555;
          Flush();
          inlined = Pop();
          next_pos_ = Pop();
          pos_ = Pop();
          slot_ = Pop();
          cell_ = Pop();
          is_inlining_ = false;
          Push(inlined);
          break;

        case Command.COMMA:
          CompileLiteral(Pop());
          break;

        default:
          throw new NotImplementedException("Command '" + cmd.ToString() + "' not implemented");
      }
    }

    public void Write(int pos, int value)
    {
      System.Diagnostics.Debug.Assert(node_ is not null);
      node_.Write(pos & 0x1ff, value);
      //node_.Write(pos, value);
    }

    /// <summary>
    /// Read from the node's memory.
    /// </summary>
    /// <param name="pos">address. may be RAM or ROM</param>
    /// <returns></returns>
    public int Read(int pos)
    {
      System.Diagnostics.Debug.Assert(node_ is not null);
      return node_.Read(pos & 0x1ff);
      //return node_.Read(pos);
    }

    /// <summary>
    /// Calculates the next address. If the address is in the I/O space, then it is not changed.
    /// </summary>
    /// <param name="addr">current address</param>
    /// <returns>next address</returns>
    static public int IncAddr(int addr)
    {
      if ((addr >= 0x100) && (addr <= 0x1ff)) { return addr; }
      if ((addr & 0x7f) == 0x7f) { return addr & ~0x7f; }
      return addr + 1;
    }

    /// <summary>
    /// Register a label.
    /// </summary>
    /// <param name="name">name of label</param>
    /// <param name="addr">address of label</param>
    /// <param name="is_rom">flag indicating tha the label belongs to the current node's ROM</param>
    /// <exception cref="InvalidDataException">there is no current node</exception>
    public void RegisterLabel(string name, int addr, bool is_rom)
    {
      if (node_ is null)
      {
        throw new InvalidDataException("no node specified for label '" + name + "'");
      }
      node_.RegisterLabel(name, addr, is_rom);
    }

    /// <summary>
    /// Register a literal.
    /// </summary>
    /// <param name="name">name of literal</param>
    /// <param name="value">value of literal</param>
    /// <param name="is_rom">flag indicating tha the literal belongs to the current node's ROM</param>
    /// <exception cref="InvalidDataException">there is no current node</exception>
    public void RegisterLiteral(string name, int value, bool is_rom)
    {
      if (node_ is null)
      {
        throw new InvalidDataException("no node specified for literal '" + name + "'");
      }
      node_.RegisterLiteral(name, value, is_rom);
    }

    /// <summary>
    /// Scan for the next word in the input stream.
    /// </summary>
    /// <param name="del">Delimiter</param>
    /// <exception cref="InvalidDataException">No delimiter found</exception>
    public void Word(char del)
    {
      if (!ScanNext(current_, ref src_pos_, out start_pos_, del, bld_))
      {
        throw new InvalidDataException("word expected");
      }
    }

    /// <summary>
    /// Fills current cell with nop's.
    /// </summary>
    /// <returns>Return true if any code was compiled or pushed</returns>
    public bool FlushNop()
    {
      switch (slot_)
      {
        case 1: CompileOpcode(F18A.Opcode.NOP); goto case 2;
        case 2: CompileOpcode(F18A.Opcode.NOP); goto case 3;
        case 3: CompileOpcode(F18A.Opcode.NOP); return true;
        default: break;
      }
      return false;
    }

    /// <summary>
    /// Finish a current cell if it is not empty.
    /// </summary>
    /// <returns>Return true if any code was compiled or pushed</returns>
    public bool Flush()
    {
      if (slot_ > 0)
      {
        if (is_inlining_)
        {
          Push(cell_);
        }
        else
        {
          Write(pos_, cell_);
        }
        pos_ = next_pos_;
        next_pos_ = IncAddr(next_pos_);
        slot_ = 0;
        cell_ = 0x15555;
        return true;
      }
      return false;
    }

    public void PatchLastCall()
    {
      switch (last_call_slot_)
      {
        case 0: last_call_cell_ &= ~0x2000; break;
        case 1: last_call_cell_ &= ~0x100; break;
        case 2: last_call_cell_ &= ~0x8; break;
        default:
          throw new InvalidDataException("invalid last call patch");
      }
      Write(last_call_pos_, last_call_cell_);
    }

    /// <summary>
    /// Add an opcode to the current cell. If there not enough room for the opcode,
    /// the current cell is flushed and the opcode is written to a new cell.
    /// If the current cell is completed, it is flushed and a new empty cell is set, ready to accept opcodes.
    /// </summary>
    /// <param name="op">Opcode to be compiled</param>
    public void CompileOpcode(F18A.Opcode op)
    {
      int opcode = (int)op;
      //if (slot_ >= 4) { Flush(); }
      if (slot_ == 3)
      {
        if ((opcode & 3) != 0) { FlushNop(); }
      }
      switch (slot_)
      {
        case 0: opcode <<= 13; break;
        case 1: opcode <<= 8; break;
        case 2: opcode <<= 3; break;
        case 3: opcode >>= 2; break;
      }
      cell_ ^= opcode;
      ++slot_;
      if (slot_ >= 4) { Flush(); }
    }

    /// <summary>
    /// Calculates the # of available address bits including current slot.
    /// </summary>
    /// <param name="slot">current slot</param>
    /// <returns># of bits left in the current cell</returns>
    static public int GetNoOfRemainingSlotBits(int slot)
    {
      switch (slot)
      {
        case 0: throw new InvalidDataException("invalid slot");
        case 1: return 10;
        case 2: return 8;
        case 3: return 3;
        default: break;
      }
      return 0;
    }

    /// <summary>
    /// Calculate the number of bits needed containing the difference between 2 values.
    /// </summary>
    /// <param name="val1">address 1</param>
    /// <param name="val2">address 2</param>
    /// <returns># of bits needed to contain difference.</returns>
    static public int HighestDifferentBit(int val1, int val2)
    {
      int res = 0;
      uint v1 = (uint)val1;
      uint v2 = (uint)val2;
      while (v1 != v2)
      {
        v1 >>= 1;
        v2 >>= 1;
        ++res;
      }
      return res;
    }

    /// <summary>
    /// Generate an address corresponding to the branch destination
    /// </summary>
    /// <param name="slot">slot of branch instruction (0..2)</param>
    /// <param name="addr">address in RAM, ROM or I/O</param>
    /// <returns>internal address</returns>
    static public int CalculateAddress(int slot, int addr)
    {
      if (slot > 0) { return addr & ~0x100; }
      return addr;
    }

    /// <summary>
    /// Compile a branch instuction to the target address
    /// </summary>
    /// <param name="op">branch opcode</param>
    /// <param name="dest_addr"></param>
    /// <exception cref="InvalidDataException"></exception>
    public void CompileBranch(F18A.Opcode op, int dest_addr)
    {
      switch (op)
      {
        case F18A.Opcode.CALL:
        case F18A.Opcode.JUMP:
        case F18A.Opcode.IF:
        case F18A.Opcode.MIF:
        case F18A.Opcode.NEXT:
          break;
        default:
          throw new InvalidDataException("invalid branch opcode '" + op.ToString() + "'");
      }
      int slot = slot_; // defines into which slot the branch instruction goes
      dest_addr = CalculateAddress(slot, dest_addr);
      int src_addr = CalculateAddress(slot, pos_);
      int diff_bit = HighestDifferentBit(src_addr, dest_addr);
      int rem_bits = GetNoOfRemainingSlotBits(slot+1);
      if ((rem_bits < diff_bit) || (slot > 2))
      {
        if (is_inlining_)
        {
          throw new InvalidDataException("inline call does not fit into cell");
        }
        FlushNop();
        slot = slot_;
        rem_bits = GetNoOfRemainingSlotBits(slot+1);
      }
      CompileOpcode(op);
      int mask = 0;
      switch (slot)
      {
        case 0: mask = 0x3ff; break;
        case 1: mask = 0xff; break;
        case 2: mask = 0x7; break;
        default:
          throw new InvalidDataException("invalid branch slot");
      }
      int addr_bits = dest_addr & mask;
      cell_ &= ~mask;
      cell_ |= addr_bits;
      last_call_pos_ = pos_;
      last_call_cell_ = cell_;
      last_call_slot_ = slot;
      slot_ = 4;
      Flush();
      new_last_was_call_ = op == F18A.Opcode.CALL;
    }

    public int PrepareBranch(F18A.Opcode op)
    {
      switch (op)
      {
        case F18A.Opcode.CALL:
        case F18A.Opcode.JUMP:
        case F18A.Opcode.IF:
        case F18A.Opcode.MIF:
        case F18A.Opcode.NEXT:
          break;
        default:
          throw new InvalidDataException("invalid branch opcode '" + op.ToString() + "'");
      }
      if (slot_ > 2) { FlushNop(); }
      int slot = slot_; // defines into which slot the branch instruction goes
      CompileOpcode(op);
      int fixup = pos_;
      int delta = next_pos_ - pos_;
      if (delta < 0)
      {
        delta += 0x40;
      }
      System.Diagnostics.Debug.Assert(delta >= 0);
      System.Diagnostics.Debug.Assert(delta <= 3);
      fixup <<= 3;
      fixup |= slot;
      fixup <<= 3;
      fixup |= delta;
      slot_ = 4;
      Flush();
      return fixup;
    }

    public void Fixup(int fixup, int dest_addr)
    {
      int delta = fixup & 0x7;
      int slot = (fixup >> 3) & 0x7; // slot of branch instruction
      int code_addr = fixup >> 6;
      int next_addr = code_addr;
      dest_addr = CalculateAddress(slot+1, dest_addr);
      while (delta > 0)
      {
        next_addr = IncAddr(next_addr);
        --delta;
      }
      next_addr = CalculateAddress(slot+1, next_addr);
      int cell = Read(code_addr);
      int slot_bits = 0;
      int mask = 0;
      switch (slot)
      {
        case 0: slot_bits = 10; mask = 0x3ff; break;
        case 1: slot_bits = 8; mask = 0xff; break;
        case 2: slot_bits = 3; mask = 0x7; break;
        default: throw new InvalidDataException("invalid slot number");
      }
      int diff = HighestDifferentBit(next_addr, dest_addr);
      if (slot_bits < diff)
      {
        //System.Diagnostics.Debug.Assert(node_ is not null);
        //Console.WriteLine("-------------- ROM -------------------");
        //Console.WriteLine(Disassemble(node_, 0x80, 0xBF));
        throw new InvalidDataException("fixup failed because destination address is unreachable");
      }
      cell &= ~mask;
      cell |= dest_addr & mask;
      Write(code_addr, cell);
    }

    public void CompileLiteral(int value)
    {
      CompileOpcode(F18A.Opcode.FETCHP);
      //Write(next_pos_, value ^ 0x15555);
      Write(next_pos_, value);
      next_pos_ = IncAddr(next_pos_);
    }

    public void CompileNumber(int value)
    {
      if (is_compiling_) { CompileLiteral(value); }
      else { Push(value); }
    }

    public string Decompile(bool is_rom)
    {
      System.Diagnostics.Debug.Assert(node_ is not null);
      StringBuilder sb = new();

      if (is_rom) { node_.DecompileROM(sb); }
      else { node_.DecompileRAM(sb); }

      F18A.Memory mem = is_rom ? node_.ROM : node_.RAM;
      int limit = (int) (is_rom ? F18A.ROM_SIZE : F18A.RAM_SIZE);
      int i = 0;
      while (i < limit)
      {
        int addr = i;
        if (is_rom) { addr += 0x80; }
        if (node_.TryLookupAddress(addr, out var lbl))
        {
          sb.Append(": ");
          sb.Append(lbl);
          sb.AppendLine();
        }
      }
      return sb.ToString();
    }
    public void Compile(string src, bool is_rom)
    {
      System.Diagnostics.Debug.Assert(node_ is not null);
      //stack_.Clear();
      rstack_.Clear();
      is_skipping_ = false;
      is_skipping_else_ = false;
      is_compiling_ = false;
      is_finished_ = false;
      src_pos_ = 0;
      current_ = src;
      pos_ = -1;
      next_pos_ = -1;
      cell_ = 0x15555;
      string word;
      int val;

      while (!is_finished_)
      {
        if (is_skipping_)
        {
          if (!ScanNext(src, ref src_pos_, out start_pos_, ' ', bld_))
          {
            throw new InvalidDataException("missing #THEN");
          }
          word = bld_.ToString();
          if (word == "#ELSE")
          {
            if (is_skipping_else_)
            {
              throw new InvalidDataException("missing #THEN");
            }

          }
          else if (word == "#THEN")
          {
            is_skipping_ = false;
            is_skipping_else_ = false;
          }
        }
        else
        {
          //Console.WriteLine("-------------- ROM -------------------");
          //Console.WriteLine(Disassemble(node_, 0x80, 0xBF));
          new_last_was_call_ = false;
          if (!ScanNext(src, ref src_pos_, out start_pos_, ' ', bld_)) { is_finished_ = true; break; }
          word = bld_.ToString();
          Command? cmd = CommandFromString(word);
          if (cmd is not null)
          {
            CompileCommand((Command)cmd, is_rom);
            last_was_call_ = new_last_was_call_;
            continue;
          }
          F18A.Opcode? op = OpcodeFromString(word);
          if (op is not null)
          {
            CompileOpcode((F18A.Opcode)op);
            last_was_call_ = new_last_was_call_;
            continue;
          }
          if (node_.TryLookupLabel(word, out val))
          {
            CompileBranch(F18A.Opcode.CALL, val);
            last_was_call_ = new_last_was_call_;
            continue;
          }
          if (node_.TryLookupLiteral(word, out val))
          {
            CompileLiteral(val);
            last_was_call_ = new_last_was_call_;
            continue;
          }
          if (constant_map_g.TryGetValue(word, out val))
          {
            CompileLiteral(val);
            last_was_call_ = new_last_was_call_;
            continue;
          }
          if (ConvertToNumber(word, out var value))
          {
            CompileNumber(value);
            last_was_call_ = new_last_was_call_;
            continue;
          }
          Console.WriteLine("-------------- ROM -------------------");
          Console.WriteLine(Disassemble(node_, 0x80, 0xBF));
          throw new InvalidDataException("? " + word);
        }
      }

      if (rstack_.Count > 0)
      {
        throw new InvalidDataException("return stack not empty");
      }
      current_ = "";
    }


  }
}
