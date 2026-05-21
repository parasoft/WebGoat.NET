using WebGoatCore.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebGoatCore.Data
{
    public class CategoryRepository
    {
        private readonly NorthwindContext _context;

        public CategoryRepository(NorthwindContext context)
        {
            _context = context;
        }

        // for unit tests only
        public CategoryRepository() { }

        public virtual List<Category> GetAllCategories()
        {
            return _context.Categories.OrderBy(c => c.CategoryId).ToList();
        }

        public virtual Category? GetById(int id)
        {
            return _context.Categories.Find(id);
        }
    }
}
