using WebGoatCore.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebGoatCore.Data
{
    public class SupplierRepository
    {
        private readonly NorthwindContext _context;

        public SupplierRepository(NorthwindContext context)
        {
            _context = context;
        }

        // for unit tests only
        public SupplierRepository() { }

        public virtual List<Supplier> GetAllSuppliers()
        {
            return _context.Suppliers.OrderBy(s => s.CompanyName).ToList();
        }
    }
}
