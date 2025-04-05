using System.Reflection.Emit;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace CsEvb
{
  public class F18A : IXmlSerializable
  {
    public const uint INVALID_VALUE = 0x80000000;

    //public const uint ParameterStackSize = 8;
    //public const uint ReturnStackSize = 8;
    public const uint STACK_SIZE = 8;

    public const int ROM_START = 0x80;
    public const uint ROM_BITS = 6;
    public const uint ROM_SIZE = 1u << (int)ROM_BITS;
    public const uint ROM_MASK = ROM_SIZE - 1;

    public const int RAM_START = 0x00;
    public const uint RAM_BITS = 6;
    public const uint RAM_SIZE = 1u << (int)RAM_BITS;
    public const uint RAM_MASK = RAM_SIZE - 1;

    public const uint MEM_BITS = 6;
    public const uint MEM_SIZE = 1u << (int)MEM_BITS;
    public const uint MEM_MASK = MEM_SIZE - 1;

    //============================================================================

    public enum Opcode
    {
      RET,
      EX,
      JUMP,
      CALL,
      UNEXT,
      NEXT,
      IF,
      MIF,
      FETCHP,
      FETCHPLUS,
      FETCHB,
      FETCHA,
      STOREP,
      STOREPLUS,
      STOREB,
      STOREA,
      PLUSMUL,
      MUL2,
      DIV2,
      INV,
      ADD,
      AND,
      XOR,
      DROP,
      DUP,
      POP,
      OVER,
      A,
      NOP,
      PUSH,
      BSTORE,
      ASTORE,
      limit
    }

    static public string? OpcodeToString(Opcode op)
    {
      switch (op)
      {
        case Opcode.RET: return "ret";
        case Opcode.EX: return "ex";
        case Opcode.JUMP: return "jump";
        case Opcode.CALL: return "call";
        case Opcode.UNEXT: return "unext";
        case Opcode.NEXT: return "next";
        case Opcode.IF: return "if";
        case Opcode.MIF: return "-if";
        case Opcode.FETCHP: return "@p";
        case Opcode.FETCHPLUS: return "@+";
        case Opcode.FETCHB: return "@b";
        case Opcode.FETCHA: return "@";
        case Opcode.STOREP: return "!p";
        case Opcode.STOREPLUS: return "!+";
        case Opcode.STOREB: return "!b";
        case Opcode.STOREA: return "!";
        case Opcode.PLUSMUL: return "+*";
        case Opcode.MUL2: return "2*";
        case Opcode.DIV2: return "2/";
        case Opcode.INV: return "inv";
        case Opcode.ADD: return "+";
        case Opcode.AND: return "and";
        case Opcode.XOR: return "xor";
        case Opcode.DROP: return "drop";
        case Opcode.DUP: return "dup";
        case Opcode.POP: return "r>";
        case Opcode.OVER: return "over";
        case Opcode.A: return "a";
        case Opcode.NOP: return "nop";
        case Opcode.PUSH: return ">r";
        case Opcode.BSTORE: return "b!";
        case Opcode.ASTORE: return "a!";
      }
      return null;
    }

    //============================================================================

    static public int GetSlot(int code, int slot)
    {
      code ^= 0x15555;
      switch (slot)
      {
        case 0: return (code >> 13) & 0x1f;
        case 1: return (code >> 8) & 0x1f;
        case 2: return (code >> 3) & 0x1f;
        case 3: return (code << 2) & 0x1f;
        default:
          throw new InvalidDataException($"invalid slot '{slot}'");
      }
    }

    static public int GetAddress(int code, int slot, int baseAddr)
    {
      int addr;
      switch (slot)
      {
        case 1: addr = code & 0x3ff; break;
        case 2: addr = (code & 0xff) | (baseAddr & 0x300); break;
        case 3: addr = (code & 0x7) | (baseAddr & 0x3f8); break;
        default:
          throw new InvalidDataException($"invalid slot '{slot}'");
      }
      return addr;
    }

    //============================================================================

    public enum Register
    {
      T,
      S,
      R,
      P,
      I,
      A,
      B,
      IO,
      UP,     // up port
      LT,     // left port
      DN,     // down port
      RT,     // right port
      limit
    }

    //============================================================================

    public enum Port
    {
      UP,
      LEFT,
      DOWN,
      RIGHT,
      limit
    }

    //============================================================================

    public enum IOBitCtrl
    {
      TRISTATE,
      WEAK_PULLDOWN,
      LOW,
      HIGH,
      limit
    }

    //============================================================================

    public enum IOBitWrite
    {
      PIN1_ctl = 0,
      PIN1_value = 1,
      PIN3_ctl = 2,
      PIN3_value = 3,
      PIN5_ctl = 4,
      PIN5_value = 5,
      WD = 11,
      PIN17_ctl = 16,
      PIN17_value = 17,
    }

    //============================================================================

    public enum IOBitAltWrite
    {
      DA_lsb = 0,
      DA_msb = 11,
      DB = 12,
      CTL = 13,
      VCO = 14,
      SR = 17,
    }

    //============================================================================

    public enum IOBitRead
    {
      PIN1 = 1,
      PIN3 = 3,
      PIN5 = 5,
      UW = 9,
      UR = 10,
      LW = 11,
      LR = 12,
      DW = 13,
      DR = 14,
      RW = 15,
      RR = 16,
      PIN17 = 17,
    }

    //============================================================================


    static public string? IOToString(int addr)
    {
      switch (addr)
      {
        case 0x15d: return "io";
        case 0x171: return "ldata";
        case 0x141: return "data";
        case 0x157: return "warp";
        case 0x175: return "left";
        case 0x115: return "down";
        case 0x1d5: return "right";
        case 0x195: return "corner";
        case 0x1b5: return "top";
        case 0x145: return "up";
        case 0x185: return "side";
        case 0x1a5: return "center";
        default: break;
      }
      return null;
    }

    //============================================================================


    //public enum IOPortAddr
    //{
    //  io = 0x08,
    //  data = 0x14,
    //  ___u = 0x10,
    //  __l_ = 0x20,
    //  __lu = 0x30,
    //  _d__ = 0x40,
    //  _d_u = 0x50,
    //  _dl_ = 0x60,
    //  _dlu = 0x70,
    //  r___ = 0x80,
    //  r__u = 0x90,
    //  r_l_ = 0xa0,
    //  r_lu = 0xb0,
    //  rd__ = 0xc0,
    //  rd_u = 0xd0,
    //  rdl_ = 0xe0,
    //  rdlu = 0xf0,
    //}


    static public string? AddrToString(int addr)
    {
      switch (addr)
      {
        case 0x15d: return "io";
        case 0x171: return "ldata";
        case 0x141: return "data";
        case 0x157: return "warp";
        case 0x175: return "--l-";
        case 0x115: return "-d--";
        case 0x135: return "-dl-";
        case 0x1d5: return "r---";
        case 0x1f5: return "r-l-";
        case 0x195: return "rd--";
        case 0x1b5: return "rdl-";
        case 0x145: return "---u";
        case 0x165: return "--lu";
        case 0x105: return "-d-u";
        case 0x125: return "-dlu";
        case 0x1c5: return "r--u";
        case 0x1e5: return "r-lu";
        case 0x185: return "rd-u";
        case 0x1a5: return "rdlu";
        default: break;
      }
      return null;
    }

    //============================================================================

    public enum RomType
    {
      undefined,
      Basic,
      Analog,
      Digital,
      SerdesBoot,
      SyncBoot,
      AsyncBoot,
      SpiBoot,
      Wire1,
      Node007,
      Node008,
      Node009,
      Node105,
      Node106,
      Node107,
      Node108,
      limit
    }

    //============================================================================

    public struct RegisterSet
    {
      private uint[] data_;

      public RegisterSet()
      {
        data_ = new uint[(int)Register.limit];
      }

      public uint[] Data { get { return data_; } }

      public uint T { get { return data_[(int)Register.T]; } set { data_[(int)Register.T] = value; } }
      public uint S { get { return data_[(int)Register.S]; } set { data_[(int)Register.S] = value; } }
      public uint R { get { return data_[(int)Register.R]; } set { data_[(int)Register.R] = value; } }
      public uint P { get { return data_[(int)Register.P]; } set { data_[(int)Register.P] = value; } }
      public uint I { get { return data_[(int)Register.I]; } set { data_[(int)Register.I] = value; } }
      public uint A { get { return data_[(int)Register.A]; } set { data_[(int)Register.A] = value; } }
      public uint B { get { return data_[(int)Register.B]; } set { data_[(int)Register.B] = value; } }
      public uint IO { get { return data_[(int)Register.IO]; } set { data_[(int)Register.IO] = value; } }
      public uint UP { get { return data_[(int)Register.UP]; } set { data_[(int)Register.UP] = value; } }
      public uint LT { get { return data_[(int)Register.LT]; } set { data_[(int)Register.LT] = value; } }
      public uint DN { get { return data_[(int)Register.DN]; } set { data_[(int)Register.DN] = value; } }
      public uint RT { get { return data_[(int)Register.RT]; } set { data_[(int)Register.RT] = value; } }

    }

    //============================================================================

    public struct Memory
    {
      private int[] data_;
      //private Dictionary<string, int> label_map_ = [];

      public Memory()
      {
        data_ = new int[MEM_SIZE];
      }

      public void Write(int pos, int value)
      {
        data_[pos & 0x3f] = value;
      }

      public int Read(int pos)
      {
        return data_[pos & 0x3f];
      }

      public void Clear()
      {
        for (int i = 0; i < MEM_SIZE; ++i)
        {
          data_[i] = 0;
        }
      }

      public void Init(int val)
      {
        for (int i = 0; i < MEM_SIZE; ++i)
        {
          data_[i] = val;
        }
      }

    }

    //============================================================================

    static private string relay = @"
  # xA1 org
  : relay ( a) r> a! @+ >r @+ zif drop ahead SWAP then r> over >r @p a relay !b !b !b begin @+ !b unext
  : done then a >r a! ;";

    static private string warm = @"
  # xA9 org
  : warm await ;";

    static private string poly = @"
  # xAA org
  : poly ( xn-xy) r> a! >r @+ a begin >r *. r> a! @+ + a next >r ;";

    static private string interp = @"
  : interp ( ims-v) dup >r >r over
    begin 2/ unext a! and >r @+ dup @+ inv . + inv r> a! dup dup xor 
    begin +* unext >r drop r> . + ;";

    static private string filter = @"
  : taps ( y x c - y' x') r> a! >r begin @+ @ >r a >r *.17 r> a! >r !+ r> . + r> next @ a! ;";

    static private string mul17 = @"
  : *.17 ( a b - a a*b) a! 16. >r dup dup xor begin +* unext inv +* a -if drop inv 2* ; then drop 2* inv ;";

    static private string shift = @"
  : lsh ( w n-1 - w') for 2* unext ;
  : rsh ( w n-1 - w') for 2/ unext ;";

    static private string triangle = @"
  : triangle ( ang*4) x1.0000 over -if drop . + ; then drop inv . + inv ;";

    static private string muldot = @"
  : *. ( f1 f2 - f1 f1*f2) *.17 a 2* -if drop inv 2* inv ; then drop 2* ;";

    static private string divide = @"
  +cy
  : clc ( -2-) dup dup xor dup . + drop ;
  : --u/mod ( hld-rq) clc
  : -u/mod ( hld-ra) a! 17. >r
    begin begin dup . + . >r dup . + dup a . + -if drop r> *next dup . + ; then over xor xor . r> next dup . + ;
  -cy";

    static private string wire1 = @"
  # x9E org
  : rcv ( s-sn) a >r dup dup a! 17. for begin
  : bit @ drop @b -if drop inv 2* inv *next r> a! ; then drop 2* next r> a! ;
  # xAA org
  : cold left center a! . io b! dup dup xB7. dup >r >r 16. >r @ drop @b # await -until drop a! . bit ;
  : fwd-b7 >r rcv a! rcv >r zif ; then begin rcv !+ next ;";


    static private string dac = @"
  : -dac ( legacy entry name ...)
  : dac27 ( m c p a w - m c p) dup >r >r over r> inv . +  >r >r  x155. r> over xor a 
    begin unext !b . begin unext !b !b ;";

    static private string spi = @"
  # x2A NL ---  # x2B NL --+  # x3A NL +--  # x3B NL +-+ # x2F NL -++
  # xC2 org
  : 8obits ( dw-dw') 7. for  ( obit) leap 2* *next ;
  : ibit ( dw-dw') @b . -if  drop inv 2* ;  then drop 2* inv ;
  : half ( dwc-dw) !b over for . . unext ;
  : select ( dw-dw) -++ half --+ half ;
  : obit ( dw-dw) then  -if +-- half +-+ half ; then
  : rbit ( dw-dw) --- half --+ half ;
  : 18ibits ( d-dw) dup 17. >r
  : ibits   begin rbit ibit inv next ;
  : u2/ ( n - n) 2/ x1FFFF. and ;
  # 497 NL spispeed
  # xC00 NL spicmd
  # 0 NL spiadr
  : cold @b inv ..  # await -until  spispeed spiadr >r spicmd
  : spi-boot ( d ah - d x) select  8obits 8obits drop r> . 8obits 8obits
  : spi-exec ( dx-dx) drop 18ibits x1E000. . +  # rdl- -until >r 18ibits a! 18ibits
  : spi-copy ( dn-dx) >r zif ; then begin 18ibits !+ next  dup ;";


    static private string async = @"
  # xCB equ 18ibits
  : cold   x31A5. a! @  @b .. -if
  : ser-exec ( x - d)   18ibits drop >r 18ibits drop a! 18ibits
  : ser-copy ( xnx-d)   drop >r zif ;  then begin 18ibits drop !+ next ;  then drop # await lit >r >r ;
  : wait ( x-1/1)   begin . drop @b -until  . drop ;
  : sync ( x-3/2-d)   dup dup wait  xor inv >r begin @b . -if . drop *next await ;  then . drop r> inv 2/ ;
  : start ( dw-4/2-dw,io) dup wait over dup 2/ . + >r
  : delay ( -1/1-io) begin @b . -if then . drop next @b ;
   ( 18ibits ( x-4/6-dwx) sync sync dup start ( 2bits) leap leap
  : byte   then drop start leap
  : 4bits   then leap
  : 2bits   then then leap
  : 1bit ( nw,io - nw,io) then >r 2/ r> over xor x20000. and xor over >r delay ;";

    static private string sync = @"
  # xBE equ sget
  : cold   x31A5. a! @ @b . . -if # await # x3.FC00 [+] lit  dup >r dup
    begin drop @b . -if ( rising) *next  SWAP then # await lit >r  drop >r ; then
  : ser-exec ( x - x)   sget >r  sget a!  sget
  : ser-copy ( n)   >r zif ;  then begin sget !+ next ;
  ( sget) ( -4/3-w)   dup leap leap
  : 6in   then then leap leap
  : 2in   then then  2* 2*  dup
  begin . drop @b . inv -until  inv 2. and dup begin . drop @b . . -until  2. and 2/ xor xor ;";

    static private string n007 = @"
  : db@ ( -w) @ ;
  : db! ( w) x15555. !b !
  : inpt x14555. !b ;
  ( assumes a=up, b=io, r=down)
  ";

    static private string n008 = @"
  # 0 equ nooop
  # 4 equ rcol1
  # x10 equ cmmd
  # x13 equ rcol
  # x15 equ wcol
  ";

    static private string n009 = @"
  : cmd ( c) x3d.555 !b .. @ !b cmd ;
  ( assumes a=right, b=data)
  ";

    static private string n105 = @"
  : rp-- ( a-a) -1. . + ;
  : 'else : bs@ ( a-w) @p !b !b A[ @p # 106 its x@ ]] ,
    @p !b @b ; A[ . . . !p ]] ,
  : rp@ ( ri-ri) over rp--
  : pshbs ( w) @p !b !b ; A[ @p # 106 its pshw ]] ,
  : 'r@ ( ri-ri) over rp--
  : @w ( a) bs@ pshbs ;
  : rfrom ( ri-r'i) over rp-- over over @w ;
  : popbs ( -w) @p !b @b ; A[ !p # 106 its pops ]] ,
  : pshr ( aw-a) @p !b !b dup A[ @p . . @p ]] ,
    !b @p !b A[ # 106 its x! ]] ,
  : rp++ : ip++ ( a-a) 1. . + ;
  : tor ( ri-r'i) >r popbs pshr r> ;
  : rp! ( i-ri) >r popbs rp++ r> ;
  : 'con ( ra-r'i) bs@
  : 'var ( ra-r'i) dup pshbs
  : 'exit ( rx-r'i) drop rp-- dup bs@ ;

  begin dup 2* -if drop !b else drop >r ex
  : bitsy then dup bs@ >r ip++
  : xxt r> -until >r pshr r> bitsy ;

  : 'ex ( xt) popbs >r xxt ;
  : 'lit ( -w) dup bs@ >r ip++ r> pshbs ;
  : 'if ( t) popbs if drop ip++ ; then drop 'else ;
  ";

    static private string n106 = @"
  # x3C org : xa@ ( a) ( @p !b !b ; @p # 107 its sd@ )
  # x3E org : xa! ( a) ( @p !b !b ; @p # 107 its sd! )
  # xAA org : 'c@ : '@ : x@ ( a-w) xa@ @b ;
  : 1+ : sp++ : char+ : cell+ ( w-w') 1. . + ;
  : popt ( p-xp't) dup sp++ over x@ ;
  : 1- : sp-- : char- : cell- ( w-w') -1. . + ;
  : psht ( pt-p') >r sp-- r> over
  : x! ( wa) xa! !b ;
  : 'c! : '! ( pwa-p'st) x!
  : popts ( p-p'st) popt
  : pops ( pt-p'st) >r popt r> ;
  : pshs ( pst-p't) >r psht r> ;
  : page@ ( pst-p'tw) @p !b @b .. dup !p ;
  : pshw ( pstw-p'tw) >r pshs r> ;
  : page! ( ptw-p'st) @p !b !b .. drop @p ; pops ;
  : sp@ ( -a) pshs psht dup pops ;
  : sp! ( a) pshs popts ;
  : 'drop ( w) drop pops ;
  : 'over ( ww-www) over pshw ;
  : 'dup ( w-ww) dup pshw ;
  : 'swap ( ab-ba) over >r >r drop r> r> ;
  : '2/ ( w-w) 2/ ;
  : '2* ( w-w) 2* ;
  : um+ ( uu-uc) over xor -if over xor . + -if
  : 'nc ( -0) dup dup xor ;
  : 'cy ( -1) then 1. ;
    then over xor -if + 'cy ; then + 'nc ;
  : zless ( n-t) -if dup xor inv ; then dup xor ;
  : 'or ( ww-w) over inv and
  : 'xor ( ww-w) xor pops ;
  : 'and ( ww-w) and pops ;
  : negate ( w-w) 1-
  : invert ( w-w) begin inv ;
  : zeq ( w-t) until dup xor ;
  : '+ ( pww-p'sw') + pops ;
  : swap- ( ww-w) inv . + inv pops ;
  ";

    static private string n107 = @"
  : a2rc ( pa-pbc) dup >r 2/ 2/ 2/ 2/ 2/ 2/ 2/ 2/ 2/ 2/ -if
  : row! ( pr-pbc) x7fff. and dup .. x18000. xor .. !
    A[ # -d-- =P @p ! # 008 its cmmd ]] lit ! ..
    x6000. and r> .. x3ff. and ; then over + row! ;
  : sd@ ( pa-p) a2rc x28400. xor xor !
    A[ # -d-- =P @p ! # 008 its rcol ]] lit ! ..
    down b! .. @p !b @b A[ @ !p ]] , r> b! !b ;
  : sd! ( pa-p) a2rc x20400. xor xor !
    A[ # -d-- =P @p ! # 008 its wcol ]] lit ! ..
    r> b! @b .. down b! .. @p !b !b ; @p # 007 its db!
  : poll ( ru-ru) io b! @b 2* 2* -if >r over .. r--- over r>
    then x800. and . if >r .. ---u r> then drop poll ;
  ";

    static private string n108 = @"
  : noop @p ! ; # 008 its nooop
  : cmd ( c) A[ # -d-- =P @p ! # 008 its cmmd ]] lit ! ! ;
  : idle ( m-m) @p ! .. # 008 its nooop x8003. cmd noop
    120. for @p ! .. # 008 its nooop
    begin @b and if @ .. @ ! ! *next idle ;
    then drop next @p ! ..  # 008 its nooop idle ;
  : init 4761. for noop next
    noop ( pre.all) x10400. cmd noop
    ( rfr.123) x8001. cmd noop noop
    ( rfr.123) x8002. cmd noop noop
    ( std.mode) x21. cmd noop noop
    ( ext.mode) x4000. cmd noop idle ;
  ";

    //static public string GetRomCode(RomType rt)
    //{
    //  StringBuilder sb = new();

    //}


    private GA144? chip_ = null;
    private RegisterSet register_ = new();
    private Memory ram_ = new();
    private Memory rom_ = new();
    private string source_ = "";
    private string comment_ = "";
    private RomType rom_type_ = RomType.undefined;
    private Dictionary<string, int> literal_map_ = [];
    private Dictionary<string, int> rom_literal_map_ = [];
    private Dictionary<string, int> label_map_ = [];
    private Dictionary<string, int> rom_label_map_ = [];
    private Dictionary<int, string> addr_to_label_map_ = [];
    private Dictionary<int, string> addr_to_rom_label_map_ = [];
    private uint initial_p_ = INVALID_VALUE; // position in source
    private int index_ = -1;
    private bool rom_compiled_ = false;

    public F18A()
    {
    }



    public int GetInitialP()
    {
      if (initial_p_ == F18A.INVALID_VALUE)
      {
        throw new InvalidOperationException($"/P not specified for node '{GetNodeNo()}'");
      }
      return (int)initial_p_;
    }

    public void SetInitialP(int value)
    {
      initial_p_ = (uint)value;
    }

    public int GetColumn()
    {
      if ((chip_ is not null) && (index_ >= 0))
      {
        chip_.GetNodePlaceFromIndex(index_, out _, out int c);
        return c;
      }
      return -1;
    }

    public int GetRow()
    {
      if ((chip_ is not null) && (index_ >= 0))
      {
        chip_.GetNodePlaceFromIndex(index_, out int r, out _);
        return r;
      }
      return -1;
    }

    public void Set(GA144 chip, int row, int column)
    {
      chip_ = chip;
      index_ = chip.GetNodeIndex(row, column);
    }

    public void SetChip(GA144 chip)
    {
      chip_ = chip;
    }

    public void SetIndex(int node_index)
    {
      index_ = node_index;
      if (chip_ is not null)
      {
        chip_.SetNode(node_index, this);
        rom_type_ = chip_.GetRomType(chip_.GetNodeNoFromIndex(node_index));
      }
    }

    public void SetRomType(RomType t)
    {
      rom_type_ = t;
    }


    public void ClearRAM()
    {
      ram_.Clear();
      label_map_.Clear();
      addr_to_label_map_.Clear();
      literal_map_.Clear();
    }

    public void ClearROM()
    {
      rom_.Init(0x134a9);
      rom_label_map_.Clear();
      addr_to_rom_label_map_.Clear();
      rom_literal_map_.Clear();
    }

    public string Source { get { return source_; } set { source_ = value; } }
    public string Comment { get { return comment_; } set { comment_ = value; } }

    public bool IsHorizontalBorder()
    {
      System.Diagnostics.Debug.Assert(chip_ is not null);
      int c = GetColumn();
      if (c < 0) { throw new InvalidDataException("no column"); }
      if (c >= chip_.GetNoOfColumns()) { throw new InvalidDataException("invalid column"); }
      return (c == 0) || (c == chip_.GetLastColumn());
    }

    public bool IsVerticalBorder()
    {
      System.Diagnostics.Debug.Assert(chip_ is not null);
      int r = GetRow();
      if (r < 0) { throw new InvalidDataException("no row"); }
      if (r >= chip_.GetNoOfRows()) { throw new InvalidDataException("invalid row"); }
      return (r == 0) || (r == chip_.GetLastColumn());
    }

    public int Warm() // return io address for warm start
    {
      if (IsHorizontalBorder())
      {
        if (IsVerticalBorder())
        {
          return 0x195;
        }
        return 0x185;
      }
      if (IsVerticalBorder())
      {
        return 0x1b5;
      }
      return 0x1a5;
    }

    public int GetNodeNo()
    {
      System.Diagnostics.Debug.Assert(chip_ is not null);
      System.Diagnostics.Debug.Assert(index_ >= 0);
      return chip_.GetNodeNoFromIndex(index_);
    }

    public void RegisterLabel(string label, int addr, bool is_rom)
    {
      if ((addr > 0xff) && (addr <= 0x1ff))
      {
        throw new InvalidDataException("invalid label address " + addr.ToString("X"));
      }

      if (is_rom)
      {
        rom_label_map_[label] = addr;
        addr_to_rom_label_map_[addr] = label;
        addr_to_rom_label_map_[addr ^ 0x40] = label;
      }
      else
      {
        label_map_[label] = addr;
        addr_to_label_map_[addr] = label;
        addr_to_label_map_[addr ^ 0x40] = label;
      }
    }

    public bool TryLookupLabel(string label, out int addr)
    {
      if (label_map_.TryGetValue(label, out addr)) { return true; }
      if (rom_label_map_.TryGetValue(label, out addr)) { return true; }
      addr = -1;
      return false;
    }

    public bool TryLookupAddress(int addr, out string label)
    {
      if (addr_to_label_map_.TryGetValue(addr, out var lbl))
      {
        label = lbl;
        return true;
      }
      if (addr_to_rom_label_map_.TryGetValue(addr, out var rom_lbl))
      {
        label = rom_lbl;
        return true;
      }
      label = "";
      return false;
    }

    public bool TryLookup(string name, out int val)
    {
      if (label_map_.TryGetValue(name, out val)) { return true; }
      if (rom_label_map_.TryGetValue(name, out val)) { return true; }
      if (literal_map_.TryGetValue(name, out val)) { return true; }
      if (rom_literal_map_.TryGetValue(name, out val)) { return true; }
      val = 0;
      return false;
    }

    public void RegisterLiteral(string label, int value, bool is_rom)
    {
      if (is_rom)
      {
        rom_literal_map_[label] = value;
      }
      else
      {
        literal_map_[label] = value;
      }
    }

    public bool TryLookupLiteral(string name, out int value)
    {
      if (literal_map_.TryGetValue(name, out value)) { return true; }
      if (rom_literal_map_.TryGetValue(name, out value)) { return true; }
      value = 0;
      return false;
    }


    public RomType ROMType { get { return rom_type_; } set { rom_type_ = value; } }
    public Memory RAM { get { return ram_; } }
    public Memory ROM { get { return rom_; } }

    public void Write(int pos, int value)
    {
      if ((pos >= RAM_START) && (pos < (RAM_START + RAM_SIZE + RAM_SIZE)))
      {
        ram_.Write(pos, value);
      }
      else if ((pos >= ROM_START) && (pos < (ROM_START + ROM_SIZE + ROM_SIZE)))
      {
        rom_.Write(pos, value);
      }
      else
      {
        throw new InvalidDataException("invalid address " + pos.ToString("X"));
      }
    }

    public int Read(int pos)
    {
      if ((pos >= RAM_START) && (pos < (RAM_START + RAM_SIZE + RAM_SIZE)))
      {
        return ram_.Read(pos);
      }
      else if ((pos >= ROM_START) && (pos < (ROM_START + ROM_SIZE + ROM_SIZE)))
      {
        return rom_.Read(pos);
      }
      return 0;
    }


    public string GetROMSource()
    {
      return GetROMSource(rom_type_);
    }


    static public string GetROMSource(RomType rom_type)
    {
      StringBuilder sb = new StringBuilder();
      switch (rom_type)
      {
        case RomType.Basic:
          sb.Append(relay);
          sb.Append(warm);
          sb.Append("\n # xB0 org");
          sb.Append(mul17);
          sb.Append(muldot);
          sb.Append(filter);
          sb.Append(interp);
          sb.Append(triangle);
          sb.Append(divide);
          sb.Append(poly);
          break;

        case RomType.Analog:
          sb.Append(relay);
          sb.Append(warm);
          sb.Append("\n # xB0 org");
          sb.Append(mul17);
          sb.Append(muldot);
          sb.Append(dac);
          sb.Append(interp);
          sb.Append(triangle);
          sb.Append(divide);
          sb.Append(poly);
          break;

        case RomType.SpiBoot:
          sb.Append(relay);
          sb.Append(warm);
          sb.Append(spi);
          break;

        case RomType.Wire1:
          sb.Append(warm);
          sb.Append(wire1);
          sb.Append(triangle);
          sb.Append(mul17);
          sb.Append(muldot);
          sb.Append(divide);
          break;

        case RomType.SerdesBoot:
          sb.Append(relay);
          sb.Append(warm);
          sb.Append("\n: cold x3141. a! x3FFFE. dup !  rdlu cold ;");
          sb.Append("\n# xB0 org");
          sb.Append(mul17);
          sb.Append(muldot);
          sb.Append(filter);
          sb.Append(interp);
          sb.Append(triangle);
          sb.Append(divide);
          break;

        case RomType.AsyncBoot:
          sb.Append(relay);
          sb.Append(warm);
          sb.Append(async);
          sb.Append(shift);
          break;

        case RomType.SyncBoot:
          sb.Append(relay);
          sb.Append(warm);
          sb.Append(sync);
          sb.Append(mul17);
          sb.Append(filter);
          sb.Append(triangle);
          break;

        case RomType.Node007:
          sb.Append(warm);
          sb.Append(n007);
          break;

        case RomType.Node008:
          sb.Append(warm);
          sb.Append(n008);
          break;

        case RomType.Node009:
          sb.Append(warm);
          sb.Append(n009);
          break;

        case RomType.Node105:
          sb.Append(warm);
          sb.Append(n105);
          break;

        case RomType.Node106:
          sb.Append(warm);
          sb.Append(n106);
          break;

        case RomType.Node107:
          sb.Append(warm);
          sb.Append(n107);
          break;

        case RomType.Node108:
          sb.Append(warm);
          sb.Append(n108);
          break;

        default: break;
      }
      return sb.ToString();
    }

    public void CompileROM(F18Assembler ass)
    {
      if (rom_compiled_) { return; }
      System.Diagnostics.Debug.Assert(chip_ is not null);
      ClearROM();
      string rom = GetROMSource();
      ass.Assemble(chip_, this, rom, true);
      rom_compiled_ = true;
    }

    public void CompileRAM(F18Assembler ass, string txt)
    {
      System.Diagnostics.Debug.Assert(chip_ is not null);
      ClearRAM();
      ass.Assemble(chip_, this, txt, false);
    }

    public int DecompileWord(StringBuilder sb, int addr, int pos)
    {
      sb.Append(addr.ToString("X3"));
      int jmpAddr, lit;
      string? dest;
      int code = Read(addr);
      sb.Append(' ');
      sb.Append(code.ToString("X5"));
      if (TryLookupAddress(addr, out var lbl))
      {
        sb.Append(" : ");
        sb.Append(lbl);
      }
      //int instr = code ^ 0x15555;
      ++pos;
      int slot = 0;
      while (slot <= 3)
      {
        Opcode opcode = (Opcode)GetSlot(code, slot);
        ++slot;
        switch (opcode)
        {
          case Opcode.JUMP:
            jmpAddr = GetAddress(code, slot, addr);
            if (TryLookupAddress(jmpAddr, out dest))
            {
              sb.Append(' ');
              sb.Append(dest);
              sb.Append(" ;");
            }
            else
            {
              sb.Append(" jump ");
              sb.Append(jmpAddr);
            }
            sb.Append(" (x");
            sb.Append(jmpAddr.ToString("X3"));
            sb.Append(')');
            slot = 4;
            break;

          case Opcode.CALL:
            jmpAddr = GetAddress(code, slot, addr);
            if (TryLookupAddress(jmpAddr, out dest))
            {
              sb.Append(' ');
              sb.Append(dest);
            }
            else
            {
              sb.Append(" call ");
              sb.Append(jmpAddr);
            }
            sb.Append(" (x");
            sb.Append(jmpAddr.ToString("X3"));
            sb.Append(')');
            slot = 4;
            break;

          case Opcode.NEXT:
            sb.Append(" next");
            jmpAddr = GetAddress(code, slot, addr);
            if (TryLookupAddress(jmpAddr, out dest))
            {
              sb.Append(' ');
              sb.Append(dest);
            }
            sb.Append(" (x");
            sb.Append(jmpAddr.ToString("X3"));
            sb.Append(')');
            slot = 4;
            break;

          case Opcode.FETCHP:
            lit = Read(++addr);
            ++pos;
            sb.Append(' ');
            sb.Append(lit);
            sb.Append(" (x");
            sb.Append(lit.ToString("X5"));
            sb.Append(')');
            break;

          case Opcode.STOREP:
            ++addr;
            sb.Append(' ');
            sb.Append(OpcodeToString(opcode));
            sb.Append(" (x");
            sb.Append(addr.ToString("X3"));
            sb.Append(')');
            ++pos;
            break;

          default:
            sb.Append(' ');
            sb.Append(OpcodeToString(opcode));
            break;
        }
      }
      sb.AppendLine();
      return pos;
    }

    public string DecompileROM()
    {
      StringBuilder sb = new();
      DecompileROM(sb);
      return sb.ToString();
    }
    public void DecompileROM(StringBuilder sb)
    {
      int i = 0;
      int limit = (int)ROM_SIZE;
      while (i < limit)
      {
        i = DecompileWord(sb, ROM_START + i, i);
      }
    }

    public string DecompileRAM()
    {
      StringBuilder sb = new();
      DecompileRAM(sb);
      return sb.ToString();
    }
    public void DecompileRAM(StringBuilder sb)
    {
      int i = 0;
      int limit = (int) RAM_SIZE;
      while (i < limit)
      {
        i = DecompileWord(sb, RAM_START + i, i);
      }
    }

    // return boot frame
    public int[] GetBootFrame()
    {
      int[] res = new int[RAM_SIZE+3];
      res[0] = GetInitialP();
      res[1] = 0;
      res[2] = (int)RAM_SIZE;
      for (int i = 0; i < RAM_SIZE; ++i)
      {
        res[i + 3] = ram_.Read(i);
      }
      return res;
    }

    public XmlSchema? GetSchema()
    {
      return null;
    }

    public void ReadXmlText(XmlReader reader, ref string val)
    {
      //if (reader.IsEmptyElement) { val = ""; return; }
      //val = reader.Value;

      val = "";
      if (reader.IsEmptyElement) { return; }

      while (reader.Read())
      {
        switch (reader.NodeType)
        {
          case XmlNodeType.Text:
          case XmlNodeType.CDATA:
            val += reader.Value;
            break;
          case XmlNodeType.EndElement:
            return;
          default: break;
        }
      }
    }

    public void ReadXml(XmlReader reader)
    {
      System.Diagnostics.Debug.Assert(chip_ is not null);
      int r = -1;
      int c = -1;
      if (reader.HasAttributes)
      {
        string? value;
        value = reader.GetAttribute("r");
        if (value is not null) { r = int.Parse(value); }
        value = reader.GetAttribute("c");
        if (value is not null) { c = int.Parse(value); }
      }
      if ((r >= 0) && (c >= 0))
      {
        chip_.SetNode(chip_.GetNodeIndex(r, c), this);
      }
      if (reader.IsEmptyElement) { return; }
      while (reader.Read())
      {
        switch (reader.NodeType)
        {
          case XmlNodeType.Element:
            if (reader.Name == "source") { ReadXmlText(reader, ref source_); }
            else if (reader.Name == "comment") { ReadXmlText(reader, ref comment_); }
            break;
          case XmlNodeType.EndElement:
            return;
          default: break;
        }
      }
    }

    public void WriteXml(XmlWriter writer)
    {
      writer.WriteStartElement("F18A");
      if ((chip_ is not null) && (index_ >= 0))
      {
        chip_.GetNodePlaceFromIndex(index_, out int r, out int c);
        writer.WriteAttributeString("r", r.ToString());
        writer.WriteAttributeString("c", c.ToString());
        if (source_.Length > 0)
        {
          writer.WriteStartElement("source");
          writer.WriteCData(source_);
          writer.WriteEndElement();
        }
        if (comment_.Length > 0)
        {
          //writer.WriteElementString("comment", comment_);
          writer.WriteStartElement("comment");
          writer.WriteCData(comment_);
          writer.WriteEndElement();
        }
      }
      writer.WriteEndElement();
    }
  }




}
