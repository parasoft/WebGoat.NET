using WebGoatCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WebGoatCore.Data
{
    public class ProductRepository
    {
        private readonly NorthwindContext _context;

        public ProductRepository(NorthwindContext context)
        {
            _context = context;
        }

        // for unit tests only
        public ProductRepository() { }

        public virtual Product GetProductById(int productId)
        {
            return _context.Products.Single(p => p.ProductId == productId);
        }

        public virtual List<Product> GetTopProducts(int numberOfProductsToReturn)
        {
            var orderDate = DateTime.Today.AddMonths(-1);
            var topProducts = _context.Orders
                .Where(o => o.OrderDate > orderDate)
                .Join(_context.OrderDetails, o => o.OrderId, od => od.OrderId, (o, od) => od)
                // Turn this query to standard LINQ expression, because EF Core can't handle the remaining part
                .AsEnumerable()
                .GroupBy(od => od.Product)
                .OrderByDescending(g => g.Sum(t => t.UnitPrice * t.Quantity))
                .Select(g => g.Key)
                .Take(numberOfProductsToReturn)
                .ToList();

            if(topProducts.Count < 4)
            {
                topProducts.AddRange(_context.Products
                    .OrderByDescending(p => p.UnitPrice)
                    .Take(numberOfProductsToReturn - topProducts.Count)
                    .ToList());
            }

            return topProducts;
        }

        public virtual List<Product> GetAllProducts()
        {
            return _context.Products.OrderBy(p => p.ProductName).ToList();
        }

        public virtual List<Product> FindNonDiscontinuedProducts(string? productName, int? categoryId)
        {
            var products = _context.Products.Where(p => !p.Discontinued);

            if (categoryId != null)
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }
            if (productName != null)
            {
                 return products.ToList().Where(p => p.ProductName.Contains(productName, StringComparison.CurrentCultureIgnoreCase)).OrderBy(p => p.ProductName).ToList();
            }

            return products.OrderBy(p => p.ProductName).ToList();
        }

        public virtual Product Update(Product product)
        {
            product = _context.Products.Update(product).Entity;
            _context.SaveChanges();
            return product;
        }

        public virtual void Add(Product product)
        {
            _context.Products.Add(product);
            _context.SaveChanges();
        }
    }
}
