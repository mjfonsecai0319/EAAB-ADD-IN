using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAABAddIn.Src.Core.Entities;

[Table("sgo_t_address_lex", Schema = "sgo")]
public class AddressLexEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ID { get; set; }

    [Column("seq")]
    [Required]
    public long Seq { get; set; }

    [Column("word")]
    [Required]
    [StringLength(255)]
    public string Word { get; set; }

    [Column("stdword")]
    [StringLength(50)]
    public string StdWord { get; set; }

    [Column("token")]
    public long? Token { get; set; }

    [Column("is_custom")]
    [Required]
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
