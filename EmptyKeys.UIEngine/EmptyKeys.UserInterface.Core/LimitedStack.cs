using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmptyKeys.UserInterface
{
    /// <summary>
    /// Implements generic limited size Stack
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LimitedStack<T> : LinkedList<T>
    {
        /// <summary>
        /// Gets or sets the maximum size.
        /// </summary>
        /// <value>
        /// The maximum size.
        /// </value>
        public int MaxSize { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LimitedStack{T}"/> class.
        /// </summary>
        public LimitedStack()
            : base()
        {
            MaxSize = -1;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LimitedStack{T}"/> class.
        /// </summary>
        /// <param name="maxSize">The maximum size.</param>
        public LimitedStack(int maxSize)
            : base()
        {
            MaxSize = maxSize;
        }

        /// <summary>
        /// Removes and returns the item at the top of the stack.
        /// </summary>
        /// <returns></returns>
        public T Pop()
        {
            if (First == null)
            {
                return default(T);
            }

            var item = First.Value;
            RemoveFirst();
            return item;
        }

        /// <summary>
        /// Returns the item at the top of the stack.
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            if (First == null)
            {
                return default(T);
            }

            var item = First.Value;            
            return item;
        }

        /// <summary>
        /// Inserts an item at the top of the Stack and removes last if count is bigger than MaxSize.
        /// </summary>
        /// <param name="item">The item.</param>
        public void Push(T item)
        {
            AddFirst(item);

            if (Count > MaxSize && MaxSize > 0)
            {
                RemoveLast();
            }
        }
    }
}
