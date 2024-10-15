using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FlowinglyTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CalculateTaxController : ControllerBase
    {
        [HttpGet()]
        public IActionResult GetTaxDetails(string blockText)
        {
            try
            {
                var extractedData = GetDataFromBlockText(blockText);

                return Ok(extractedData);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message); // to throw execption on missing <total>/<cost_centre>/missing tags
            }
        }

        private TagDataResponse GetDataFromBlockText(string blockText)
        {
            var response = new TagDataResponse
            {
                TagData = new Dictionary<string, string>()
            };

            // Get all the opening and closing tags from the input block text
            var openingTagRegex = @"<(\w+)>";
            var closingTagRegex = @"<\/(\w+)>";

            var openingTagList = Regex.Matches(blockText, openingTagRegex)
                                   .Cast<Match>()
                                   .Select(m => new { TagName = m.Groups[1].Value, TagIndex = m.Index })
                                   .ToList();

            var closingTagList = Regex.Matches(blockText, closingTagRegex)
                                   .Cast<Match>()
                                   .Select(m => new { TagName = m.Groups[1].Value, TagIndex = m.Index })
                                   .ToList();

            // Checking if all opening tags have a closing tag for it.
            if (openingTagList.Count != closingTagList.Count)
                throw new InvalidOperationException("There is a missmatch in the openning and closing tags");

            // get data from each tags
            for (int i = 0; i < openingTagList.Count; i++)
            {
                var openingTag = openingTagList[i];
                var closingTag = closingTagList.FirstOrDefault(p => p.TagName == openingTag.TagName && p.TagIndex > openingTag.TagIndex);

                // Extract value between the opening and closing tag
                var startIndex = openingTag.TagIndex + openingTag.TagName.Length + 2; // Adding +2 to the lenght for the <> in tag
                var valueLength = closingTag.TagIndex - startIndex;
                var tagValue = blockText.Substring(startIndex, valueLength);

                // set each tag values to response
                if(!Regex.IsMatch(tagValue,openingTagRegex))//To skip if the tag has another tag like in the first example  
                    response.TagData[openingTag.TagName] = tagValue;

                //check if the total tag is null or has a number value.
                if (openingTag.TagName == "total" && decimal.TryParse(tagValue, out decimal total))
                {
                    const decimal taxRate = 0.8m;// asssuming tax as 8% of the total this can be passed as a parameter; 
                    response.SalesTax = total * taxRate / (1 + taxRate);
                    response.TotalExcludingTax = total - response.SalesTax;
                }
            }

            // Check if <total> is missing and throw an exception
            if (!response.TagData.ContainsKey("total"))
                throw new InvalidOperationException("Tag <total> not found.");

            // check and hanndle value for <cost_centre>
            if (!response.TagData.ContainsKey("cost_centre"))
                response.TagData["cost_centre"] = "UNKNOWN";//setting as the detfault value as menbtioned in the sttory.

            return response;
        }
    }
}

public class TagDataResponse
{
    public Dictionary<string, string> TagData { get; set; }
    public decimal SalesTax { get; set; }
    public decimal TotalExcludingTax { get; set; }
}
