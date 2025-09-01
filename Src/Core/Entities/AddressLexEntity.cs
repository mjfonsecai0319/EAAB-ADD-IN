using System;

namespace EAABAddIn.Src.Core.Entities
{
    public class AddressLexEntity
    {
        public long ID { get; set; }
        public long Seq { get; set; }
        public string Word { get; set; }
        public string StdWord { get; set; }
        public long? Token { get; set; }
        public bool IsCustom { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is not AddressLexEntity other)
            {
                return false;
            }

            return ID == other.ID
                && Seq == other.Seq
                && Word == other.Word
                && StdWord == other.StdWord
                && Token == other.Token
                && IsCustom == other.IsCustom;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Seq, Word, StdWord, Token, IsCustom);
        }
    }
}
