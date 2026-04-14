using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.Enums;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class EconomyController : ControllerBase
    {
        public record AddShopItemRequest(int ItemId, int Price, string ShopTabCodeName, int Data, int CurrencyType = 0);
        public record EditQuestRewardsRequest(RewardType Type, decimal Multiplier, int MinLevel, int MaxLevel);
        public record AlchemyRatesRequest(int Param2, int Param3, int Param4);
        public record TalismanDropRequest(TalismanGroup Group, decimal DropRatio, bool AffectAll, int DropAmountMin = 1, int DropAmountMax = 1);
        public record AddMonsterDropRequest(int MonsterId, int ItemId, decimal DropRatio);
        public record ItemMaxStackRequest(string ItemCodeName, int NewStackSize);

        // POST api/economy/shop/add-item
        [HttpPost("shop/add-item")]
        public async Task<IActionResult> AddItemToShop([FromBody] AddShopItemRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.ShopTabCodeName))
                return BadRequest(new { message = "Shop tab code name is required." });

            var (success, reason) = await DBConnect.AddItemToShop(body.ItemId, body.Price, body.ShopTabCodeName, body.Data, body.CurrencyType);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = "Item added to shop.", detail = reason });
        }

        // POST api/economy/quests/rewards
        [HttpPost("quests/rewards")]
        public async Task<IActionResult> EditQuestRewards([FromBody] EditQuestRewardsRequest body)
        {
            if (body.Multiplier <= 0)
                return BadRequest(new { message = "Multiplier must be greater than 0." });

            var (success, reason) = await DBConnect.EditQuestRewardsBetweenLevel(body.Type, body.Multiplier, body.MinLevel, body.MaxLevel);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // POST api/economy/alchemy/rates
        [HttpPost("alchemy/rates")]
        public async Task<IActionResult> SetAlchemyRates([FromBody] AlchemyRatesRequest body)
        {
            var (success, reason) = await DBConnect.SetAlchemyRates(body.Param2, body.Param3, body.Param4);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // POST api/economy/talisman/drop-rates
        [HttpPost("talisman/drop-rates")]
        public async Task<IActionResult> SetTalismanDropRates([FromBody] TalismanDropRequest body)
        {
            var (success, reason) = await DBConnect.ChangeFWDropRates(body.Group, body.DropRatio, body.AffectAll, body.DropAmountMin, body.DropAmountMax);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // POST api/economy/monster/add-drop
        [HttpPost("monster/add-drop")]
        public async Task<IActionResult> AddMonsterDrop([FromBody] AddMonsterDropRequest body)
        {
            var (success, reason) = await DBConnect.AddItemToMonsterDrop(body.MonsterId, body.ItemId, body.DropRatio);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // PUT api/economy/items/max-stack
        [HttpPut("items/max-stack")]
        public async Task<IActionResult> SetItemMaxStack([FromBody] ItemMaxStackRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.ItemCodeName))
                return BadRequest(new { message = "Item code name is required." });

            var (success, reason) = await DBConnect.ChangeItemMaxStack(body.ItemCodeName, body.NewStackSize);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // GET api/economy/items/durability?code=
        [HttpGet("items/durability")]
        public async Task<IActionResult> GetItemDurability([FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { message = "Item code is required." });

            var (success, items, reason) = await DBConnect.GetItemDurability(code);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(items?.Select(i => new { codeName = i.CodeName128, durability = i.Dur_L, id = i.ID, maxStack = i.MaxStack }));
        }
    }
}
